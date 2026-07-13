/*
    Credit to JSMPEG, this is more or less a crappy hand ported of it to C# with a fancy coat of paint.
*/

using System.Runtime.CompilerServices;
using Raylib_cs;

namespace CPEG {
    public class CrapPEG {

        private BitReader _reader;

        public int width;
        public int height;
        public int screenArea; 
        
        public bool sequenceStarted = false;
        public bool isLooping = false;
        public bool isPlaying = false;

        public double frameRate = 30;
        public double lateTime = 0;
        public int firstSequenceHeader = 0;
        public double targetTime = 0;

        // MB stand for Macroblock.
        public int mbWidth; 
        public int mbHeight; 
        public int mbSize;

        public int codedWidth;
        public int codedHeight;
        public int codedSize;

        public int halfWidth;
        public int halfHeight;
        public int quarterSize;

        public int macroblockAddress = 0;
        public int mbRow = 0;
        public int mbCol = 0;

        public int macroblockType = 0;
        public bool macroblockIntra = false;
        public bool macroblockMotFw = false;

        public int motionFwH = 0;
        public int motionFwV = 0;
        public int motionFwHPrev = 0;
        public int motionFwVPrev = 0;

        public int dcPredictorY;
        public int dcPredictorCr;
        public int dcPredictorCb;

        public int quantizerScale = 0;
        public bool sliceBegin = false;

        int pictureCodingType = 0;

        public bool fullPelForward = false;
        public int forwardFCode = 0;
        public int forwardRSize = 0;
        public int forwardF = 0;

        public int[] intraQuantMatrix = new int[CrapPEGConstants.DEFAULT_INTRA_QUANT_MATRIX.Length];
        public int[] nonIntraQuantMatrix = new int[CrapPEGConstants.DEFAULT_NON_INTRA_QUANT_MATRIX.Length];

        public int[] customIntraQuantMatrix = new int[64];
        public int[] customNonIntraQuantMatrix = new int[64];
        public int[] blockData = new int[64];

        public byte[] currentY; // LUMA
        public byte[] currentCr; // CHROMA-RED
        public byte[] currentCb; // CHROMA-BLUE

        public byte[] forwardY;
        public byte[] forwardCr; 
        public byte[] forwardCb; 

        public Color[] currentRGBA;

        public CrapPEG(BitReader reader) {
            _reader = reader;

            FindStartCode(CrapPEGConstants.START_SEQUENCE);
            firstSequenceHeader = _reader.Index;
            DecodeSequenceHeader();

            // Spent 20 minutes trying to figure out a flicker i
            currentRGBA = new Color[width * height];

            NextFrame();

            QuickInfo();
        }

        // This is just to provide some quick info on the video playing.
        public void QuickInfo() {
            Console.WriteLine($"File: {_reader.Path}");       
            Console.WriteLine($"Resolution: {width}x{height}");     
            Console.WriteLine($"Framerate: {frameRate}fps");
        }

        public static void FillArray(int[] a, byte value)
        {
            for(int i = 0, length = a.Length; i < length; i++ ) {
                a[i] = value;
            }
        }

        public int FindStartCode(int code) {
            int current = 0;

            while(true) {
                current = _reader.FindNextMPEGStartCode();
                if( current == code || current == BitReader.NOT_FOUND ) {
                    return current;
                }
            }
            //return BitReader.NOT_FOUND;
        }

        public int ReadCode(byte[] codeTable)
        {
            int state = 0;

            do {
                int bit = _reader.GetBits(1);

                if(bit == CrapPEGConstants.END_OF_FILE)
                    return CrapPEGConstants.END_OF_FILE;

                state = codeTable[state + bit];
            } while(state >= 0 && codeTable[state] != 0 );
            
            return codeTable[state + 2];
        }  

        public int ReadCode(short[] codeTable)
        {
            var state = 0;
            do {
                state = codeTable[state + _reader.GetBits(1)];
            } while( state >= 0 && codeTable[state] != 0 );
            return codeTable[state+2];
        }  

        public int ReadCode(int[] codeTable)
        {
            var state = 0;
            do {
                int bits = _reader.GetBits(1); 

                /*
                    For some reason for CERTAIN videos,
                    when at the very end of the video 
                    FindNextMPEGStartCode() will not 
                    be able to properly catch that it is
                    the end, so it'll overflow instead of
                    stopping. The fix this the easy way
                    we just check here. We probbaly
                    could handle it in a more proper fahsion
                    , but I am lazy.
                */
                if(bits == CrapPEGConstants.END_OF_FILE)
                {
                    
                }


                state = codeTable[state + bits];
            } while( state >= 0 && codeTable[state] != 0 );
            return codeTable[state+2];
        }  


        public void DecodeSequenceHeader() {
            width = _reader.GetBits(12);
            height = _reader.GetBits(12);

            screenArea = width * height;

            // Skip over a SOME crap we don't need.
            _reader.AdvanceBits(4); 
            
            // MPEG-1 has a fixed table to save a couple of bits.
            frameRate = CrapPEGConstants.FRAME_RATE[_reader.GetBits(4)];

            // Skip over a bunch of crap we don't need.
            _reader.AdvanceBits(30); 

            intraQuantMatrix = CrapPEGConstants.DEFAULT_INTRA_QUANT_MATRIX;
	        nonIntraQuantMatrix = CrapPEGConstants.DEFAULT_NON_INTRA_QUANT_MATRIX;

            mbWidth = (width + 15) >> 4;
            mbHeight = (height + 15) >> 4;
            mbSize = mbWidth * mbHeight;

            codedWidth = mbWidth << 4;
            codedHeight = mbHeight << 4;
            codedSize = codedWidth * codedHeight;

            halfWidth = mbWidth << 3;
            halfHeight = mbHeight << 3;
            quarterSize = codedSize >> 2;

            // I think (atleast according to JSMPEG) they can be custom qaunt matrixes?
            if(_reader.GetBits(1) == 1) { 
                for( var i = 0; i < 64; i++ )
                    customIntraQuantMatrix[CrapPEGConstants.ZIG_ZAG[i]] = _reader.GetBits(8);
                
                intraQuantMatrix = customIntraQuantMatrix;
            }

            if(_reader.GetBits(1) == 1) { 
                for( var i = 0; i < 64; i++ )
                    customNonIntraQuantMatrix[CrapPEGConstants.ZIG_ZAG[i]] = _reader.GetBits(8);
                
                nonIntraQuantMatrix = customNonIntraQuantMatrix;
            }

            if(sequenceStarted) 
                return;

            currentY = new byte[codedSize];
            currentCr = new byte[codedSize >> 2];
            currentCb = new byte[codedSize >> 2];
            
            forwardY = new byte[codedSize];
            forwardCr = new byte[codedSize >> 2];
            forwardCb = new byte[codedSize >> 2];

        }

        public void YCbCrToRGB()
        {
            int yIndex = 0;
            int cIndex = 0; 

            int padding = 2;
        
            byte cy, cr = 128, cb = 128;

            /*
                This is based around an eariler version of JS-MPEG!

                Much less effiecent but kind of work!

                https://github.com/phoboslab/jsmpeg/blob/170a24e110ffc87afe9406b7bbd471b3f2d3d4f7/jsmpg.js#L356
            */
            for (int pixel = 0; pixel < screenArea; pixel++) {
                
                if ((pixel % width % padding == 0) && pixel / width % padding == 0)
                {
                    cb = currentCb[cIndex];
                    cr = currentCr[cIndex];

                    cIndex++;
                }

                cy = currentY[yIndex++];
                
                int blue = cb - 128;
                int red = cr - 128;

                int r = cy + (int)(1.4f * red);
                int g = cy - (int)(-0.343f * blue + 0.711f * red);
                int b = cy + (int)(1.765f * blue);
                
                currentRGBA[pixel] = new Color((byte)r, (byte)g, (byte)b);
            }
        }

        public void DecodePicture() {
            _reader.AdvanceBits(10);
            pictureCodingType = _reader.GetBits(3);
           _reader.AdvanceBits(16); 
            
            // Skip B and D frames or unknown coding type
            if(pictureCodingType <= 0 || pictureCodingType >= CrapPEGConstants.PICTURE_TYPE_B ) {
                return;
            }
            
            // full_pel_forward, forward_f_code
            if(pictureCodingType == CrapPEGConstants.PICTURE_TYPE_P ) {
                fullPelForward = _reader.GetBits(1) != 1;
                forwardFCode =  _reader.GetBits(3);
               
                if( forwardFCode == 0 ) {
                    // Ignore picture with zero forward_f_code
                    return;
                }

                forwardRSize = forwardFCode - 1;
                forwardF = 1 << forwardRSize;
            }
            
            var code = 0;
            do {
                code = _reader.FindNextMPEGStartCode();
            } while( code == CrapPEGConstants.START_EXTENSION || code == CrapPEGConstants.START_USER_DATA );
            
            
            while( code >= CrapPEGConstants.START_SLICE_FIRST && code <= CrapPEGConstants.START_SLICE_LAST ) {
                DecodeSlice(code & 0x000000FF );
                code = _reader.FindNextMPEGStartCode();
            }
            
            // We found the next start code; rewind 32bits and let the main loop handle it.
            _reader.RewindBits(32);
            
    
            YCbCrToRGB();
            
            // If this is a reference picutre then rotate the prediction pointers
            if( pictureCodingType == CrapPEGConstants.PICTURE_TYPE_I || pictureCodingType == CrapPEGConstants.PICTURE_TYPE_P ) {
                byte[] 
                    tmpY = forwardY,
                    tmpCr = forwardCr,
                    tmpCb = forwardCb;

                forwardY = currentY;
                forwardCr = currentCr;
                forwardCb = currentCb;

                currentY = tmpY;
                currentCr = tmpCr;
                currentCb = tmpCb;
            }
        }

        public void DecodeSlice(int slice) {	
            sliceBegin = true;
            macroblockAddress = (slice - 1) * mbWidth - 1;
            
            // Reset motion vectors and DC predictors
            motionFwH = motionFwHPrev = 0;
            motionFwV = motionFwVPrev = 0;
            dcPredictorY  = 128;
            dcPredictorCr = 128;
            dcPredictorCb = 128;
            
            quantizerScale = _reader.GetBits(5);
            
            // skip extra bits
            while(_reader.GetBits(1) == 1) {
                _reader.AdvanceBits(8);
            }

            do {
                DecodeMacroblock();

            } while(!_reader.NextBytesAreStartCode() );
        }

        public void DecodeMacroblock() {

            // Decode macroblock_address_increment
            int 
                increment = 0,
                t = ReadCode(CrapPEGConstants.MACROBLOCK_ADDRESS_INCREMENT);
            
            while( t == 34 ) {
                // macroblock_stuffing
                t = ReadCode(CrapPEGConstants.MACROBLOCK_ADDRESS_INCREMENT);
            }
            while( t == 35 ) {
                // macroblock_escape
                increment += 33;
                t = ReadCode(CrapPEGConstants.MACROBLOCK_ADDRESS_INCREMENT);
            }
            increment += t;

            // Process any skipped macroblocks
            if(sliceBegin ) {
                // The first macroblock_address_increment of each slice is relative
                // to beginning of the preverious row, not the preverious macroblock
                sliceBegin = false;
                macroblockAddress += increment;
            }
            else {
                if(macroblockAddress + increment >= mbSize ) {
                    // Illegal (too large) macroblock_address_increment
                    return;
                }
                if( increment > 1 ) {
                    // Skipped macroblocks reset DC predictors
                    dcPredictorY  = 128;
                    dcPredictorCr = 128;
                    dcPredictorCb = 128;
                    
                    // Skipped macroblocks in P-pictures reset motion vectors
                    if( pictureCodingType == CrapPEGConstants.PICTURE_TYPE_P ) {
                        motionFwH = motionFwHPrev = 0;
                        motionFwV = motionFwVPrev = 0;
                    }
                }
                
                // Predict skipped macroblocks
                while(increment > 1) {
                    macroblockAddress++;
                    mbRow = (macroblockAddress / mbWidth)|0;
                    mbCol = macroblockAddress % mbWidth;
                    CopyMacroblock(motionFwH, motionFwV, forwardY, forwardCr, forwardCb);
                    increment--;
                }
                macroblockAddress++;
            }

            mbRow = (macroblockAddress / mbWidth)|0;
            mbCol = macroblockAddress % mbWidth;

            // Process the current macroblock
            macroblockType = ReadCode(CrapPEGConstants.MACROBLOCK_TYPE_TABLES[pictureCodingType]);
            macroblockIntra = (macroblockType & 0x01) != 0;
            macroblockMotFw = (macroblockType & 0x08) != 0;


            // Quantizer scale
            if((macroblockType & 0x10) != 0 ) {
                quantizerScale = _reader.GetBits(5);
            }

            if(macroblockIntra ) {
                // Intra-coded macroblocks reset motion vectors
                motionFwH = motionFwHPrev = 0;
                motionFwV = motionFwVPrev = 0;
            } else {
                // Non-intra macroblocks reset DC predictors
                dcPredictorY = 128;
                dcPredictorCr = 128;
                dcPredictorCb = 128;
                
                DecodeMotionVectors();
                CopyMacroblock(motionFwH, motionFwV, forwardY, forwardCr, forwardCb);
            }

            // Decode blocks
            var cbp = ((macroblockType & 0x02) != 0) 
                ? ReadCode(CrapPEGConstants.CODE_BLOCK_PATTERN) 
                : (macroblockIntra ? 0x3f : 0);

            for(int block = 0, mask = 0x20; block < 6; block++ ) {
                if( (cbp & mask) != 0 ) {
                    DecodeBlock(block);
                }
                mask >>= 1;
            }
        }

        public void DecodeMotionVectors() {
            int code, d, r = 0;
            
            // Forward
            if(macroblockMotFw ) {
                // Horizontal forward
                code = ReadCode(CrapPEGConstants.MOTION);
                if( (code != 0) && (forwardF != 1) ) {
                    r = _reader.GetBits(forwardRSize);
                    d = ((Math.Abs(code) - 1) << forwardRSize) + r + 1;
                    if( code < 0 ) {
                        d = -d;
                    }
                } else {
                    d = code;
                }
                
                motionFwHPrev += d;
                if( motionFwHPrev > (forwardF << 4) - 1 ) {
                    motionFwHPrev -= forwardF << 5;
                }
                else if( motionFwHPrev < ((-forwardF) << 4) ) {
                    motionFwHPrev += forwardF << 5;
                }
                
                motionFwH = motionFwHPrev;
                if( fullPelForward ) {
                    motionFwH <<= 1;
                }
                
                // Vertical forward
                code = ReadCode(CrapPEGConstants.MOTION);
                if( (code != 0) && (forwardF != 1) ) {
                    r = _reader.GetBits(forwardRSize);
                    d = ((Math.Abs(code) - 1) << forwardRSize) + r + 1;
                    if( code < 0 ) {
                        d = -d;
                    }
                } else {
                    d = code;
                }
                
                motionFwVPrev += d;
                if( motionFwVPrev > (forwardF << 4) - 1 ) {
                    motionFwVPrev -= forwardF << 5;
                } else if( motionFwVPrev < ((-forwardF) << 4) ) {
                    motionFwVPrev += forwardF << 5;
                }
                
                motionFwV = motionFwVPrev;
                if( fullPelForward ) {
                    motionFwV <<= 1;
                }
            }
            else if(pictureCodingType == CrapPEGConstants.PICTURE_TYPE_P ) {
                // No motion information in P-picture, reset vectors
                motionFwH = motionFwHPrev = 0;
                motionFwV = motionFwVPrev = 0;
            }
        }

        public void CopyMacroblock(int motionH, int motionV, byte[] sY, byte[] sCr, byte[] sCb) {
           
            int width, scan, 
                H, V,
                src, dest, last;
            
            bool oddH, oddV;

            var dY = currentY;
            var dCb = currentCb;
            var dCr = currentCr;

            // Luminance
            width = codedWidth;
            scan = width - 16;
            
            H = motionH >> 1;
            V = motionV >> 1;
            oddH = (motionH & 1) == 1;
            oddV = (motionV & 1) == 1;
            
            src = ((mbRow << 4) + V) * width + (mbCol << 4) + H;
            dest = (mbRow * width + mbCol) << 4;
            last = dest + (width << 4);


            if( oddH ) {
                if( oddV ) {
                    while( dest < last ) {
                        for( var x = 0; x < 16; x++ ) {
                            dY[dest] = (byte) ((sY[src] + sY[src+1] + sY[src+width] + sY[src+width+1] + 2) >> 2);
                            dest++; src++;
                        }
                        dest += scan; src += scan;
                    }
                }
                else {
                    while( dest < last ) {
                        for( var x = 0; x < 16; x++ ) {
                            dY[dest] = (byte) ((sY[src] + sY[src+1] + 1) >> 1);
                            dest++; src++;
                        }
                        dest += scan; src += scan;
                    }
                }
            }
            else {
                if( oddV ) {
                    while( dest < last ) {
                        for( var x = 0; x < 16; x++ ) {
                            dY[dest] = (byte) ((sY[src] + sY[src+width] + 1) >> 1);
                            dest++; src++;
                        }
                        dest += scan; src += scan;
                    }
                }
                else {
                    while( dest < last ) {
                        for( var x = 0; x < 16; x++ ) {
                            dY[dest] = sY[src];
                            dest++; src++;
                        }
                        dest += scan; src += scan;
                    }
                }
            }
            
            
            // Chrominance
            
            width = halfWidth;
            scan = width - 8;
            
            H = (motionH/2) >> 1;
            V = (motionV/2) >> 1;
            oddH = ((motionH/2) & 1) == 1;
            oddV = ((motionV/2) & 1) == 1;
            
            src = ((mbRow << 3) + V) * width + (mbCol << 3) + H;
            dest = (mbRow * width + mbCol) << 3;
            last = dest + (width << 3);
            
            if( oddH ) {
                if( oddV ) {
                    while( dest < last ) {
                        for( var x = 0; x < 8; x++ ) {
                            dCr[dest] = (byte) ((sCr[src] + sCr[src+1] + sCr[src+width] + sCr[src+width+1] + 2) >> 2);
                            dCb[dest] = (byte) ((sCb[src] + sCb[src+1] + sCb[src+width] + sCb[src+width+1] + 2) >> 2);
                            dest++; src++;
                        }
                        dest += scan; src += scan;
                    }
                }
                else {
                    while( dest < last ) {
                        for( var x = 0; x < 8; x++ ) {
                            dCr[dest] = (byte) ((sCr[src] + sCr[src+1] + 1) >> 1);
                            dCb[dest] = (byte) ((sCb[src] + sCb[src+1] + 1) >> 1);
                            dest++; src++;
                        }
                        dest += scan; src += scan;
                    }
                }
            }
            else {
                if( oddV ) {
                    while( dest < last ) {
                        for( var x = 0; x < 8; x++ ) {
                            dCr[dest] = (byte) ((sCr[src] + sCr[src+width] + 1) >> 1);
                            dCb[dest] = (byte) ((sCb[src] + sCb[src+width] + 1) >> 1);
                            dest++; src++;
                        }
                        dest += scan; src += scan;
                    }
                }
                else {
                    while( dest < last ) {
                        for( var x = 0; x < 8; x++ ) {
                            dCr[dest] = sCr[src];
                            dCb[dest] = sCb[src];
                            dest++; src++;
                        }
                        dest += scan; src += scan;
                    }
                }
            }
        }

        public void DecodeBlock(int block) {
            
            var n = 0;
            
            int[] quantMatrix = new int[intraQuantMatrix.Length];
            
            // Clear preverious data
            FillArray(blockData, 0);
            
            // Decode DC coefficient of intra-coded blocks
            if(macroblockIntra) {
                int 
                    predictor,
                    dctSize;
                
                // DC prediction
                
                if( block < 4 ) {
                    predictor = dcPredictorY;
                    dctSize = ReadCode(CrapPEGConstants.DCT_DC_SIZE_LUMINANCE);
                }
                else {
                    predictor = block == 4 ? dcPredictorCr : dcPredictorCb;
                    dctSize = ReadCode(CrapPEGConstants. DCT_DC_SIZE_CHROMINANCE);
                }
                
                // Read DC coeff
                if( dctSize > 0 ) {
                    var differential = _reader.GetBits(dctSize);
                    if( (differential & (1 << (dctSize - 1))) != 0 ) {
                        blockData[0] = predictor + differential;
                    }
                    else {
                        blockData[0] = predictor + ((-1 << dctSize)|(differential+1));
                    }
                }
                else {
                    blockData[0] = predictor;
                }
                
                // Save predictor value
                if( block < 4 ) {
                    dcPredictorY = blockData[0];
                }
                else if( block == 4 ) {
                    dcPredictorCr = blockData[0];
                }
                else {
                    dcPredictorCb = blockData[0];
                }
                
                // Dequantize + premultiply
                blockData[0] <<= 3 + 5;
                
                quantMatrix = intraQuantMatrix;
                n = 1;
            }
            else {
                quantMatrix = nonIntraQuantMatrix;
            }
            
            // Decode AC coefficients (+DC for non-intra)
            int level = 0;
            while(true) {
               
                int run = 0,
                    coeff = ReadCode(CrapPEGConstants.DCT_COEFF);
                
                if((coeff == 0x0001) && (n > 0) && (_reader.GetBits(1) == 0) ) {
                    break;
                }

                if(coeff == 0xffff ) {
                    run = _reader.GetBits(6);
                    level = _reader.GetBits(8);

                    if( level == 0 ) {
                        level = _reader.GetBits(8);
                    } else if( level == 128 ) {
                        level = _reader.GetBits(8) - 256;
                    } else if( level > 128 ) {
                        level -= 256;
                    }
                }
                else {
                    run = coeff >> 8;
                    level = coeff & 0xff;

                    if(_reader.GetBits(1) == 1) {
                        level = -level;
                    }
                }
                
                n += run;

                int dezigZagged = 12;
                if(n < 64)
                    dezigZagged = CrapPEGConstants.ZIG_ZAG[n];

                n++;
                
                // Dequantize, oddify, clip
                level <<= 1;

                if(!macroblockIntra ) {
                    level += level < 0 ? -1 : 1;
                }

                level = (level * quantizerScale * quantMatrix[dezigZagged]) >> 4;
              
                if( (level & 1) == 0 ) {
               
                    level -= level > 0 ? 1 : -1;
                }

                if( level > 2047 ) {
                    level = 2047;
                } else if( level < -2048 ) {
                    level = -2048;
                }

                // Save premultiplied coefficient
                blockData[dezigZagged] = level * CrapPEGConstants.PREMULTIPLIER_MATRIX[dezigZagged];
            };
            
            // Transform block data to the spatial domain
            if( n == 1 ) {
                // Only DC coeff., no IDCT needed
                FillArray(blockData, (byte)((blockData[0] + 128) >> 8));
            }
            else {
                IDCT();
            }
            
            // Move block to its place
            int
                destIndex,
                scan;
            
            byte[] destArray = new byte[currentY.Length];

            if( block < 4 ) {
                destArray = currentY;
                scan = codedWidth - 8;
                destIndex = (mbRow * codedWidth + mbCol) << 4;
                if( (block & 1) != 0 ) {
                    destIndex += 8;
                }
                if( (block & 2) != 0 ) {
                    destIndex += codedWidth << 3;
                }
            }
            else {
                destArray = (block == 4) ? currentCb : currentCr;
                scan = (codedWidth >> 1) - 8;
                destIndex = ((mbRow * codedWidth) << 2) + (mbCol << 3);
            }
            
            n = 0;
            
            if( macroblockIntra ) {
                var mult = 0;
                // Overwrite (no prediction)
                for( var i = 0; i < 8; i++ ) {
                    for( var j = 0; j < 8; j++ ) {
                        destArray[destIndex++] = (byte) blockData[n++];
                    }
                    destIndex += scan;
                }
            }
            else {
                // Add data to the predicted macroblock
                for( var i = 0; i < 8; i++ ) {
                    for( var j = 0; j < 8; j++ ) {
                        destArray[destIndex++] += (byte) blockData[n++];
                    }
                    destIndex += scan;
                }
            }
        }


        public void IDCT() {

            int b1, b3, b4, b6, b7, tmp1, tmp2, m0,
            x0, x1, x2, x3, x4, y3, y4, y5, y6, y7,
            i;

            // Transform columns.
            for( i = 0; i < 8; ++i ) {
                b1 =  blockData[4*8+i];
                b3 =  blockData[2*8+i] + blockData[6*8+i];
                b4 =  blockData[5*8+i] - blockData[3*8+i];
                tmp1 = blockData[1*8+i] + blockData[7*8+i];
                tmp2 = blockData[3*8+i] + blockData[5*8+i];
                b6 = blockData[1*8+i] - blockData[7*8+i];
                b7 = tmp1 + tmp2;
                m0 =  blockData[0*8+i];
                x4 =  ((b6*473 - b4*196 + 128) >> 8) - b7;
                x0 =  x4 - (((tmp1 - tmp2)*362 + 128) >> 8);
                x1 =  m0 - b1;
                x2 =  (((blockData[2*8+i] - blockData[6*8+i])*362 + 128) >> 8) - b3;
                x3 =  m0 + b1;
                y3 =  x1 + x2;
                y4 =  x3 + b3;
                y5 =  x1 - x2;
                y6 =  x3 - b3;
                y7 = -x0 - ((b4*473 + b6*196 + 128) >> 8);

                blockData[0*8+i] =  b7 + y4;
                blockData[1*8+i] =  x4 + y3;
                blockData[2*8+i] =  y5 - x0;
                blockData[3*8+i] =  y6 - y7;
                blockData[4*8+i] =  y6 + y7;
                blockData[5*8+i] =  x0 + y5;
                blockData[6*8+i] =  y3 - x4;
                blockData[7*8+i] =  y4 - b7;
            }
            
            // Transform rows.
            for( i = 0; i < 64; i += 8 ) {
                b1 =  blockData[4+i];
                b3 =  blockData[2+i] + blockData[6+i];
                b4 =  blockData[5+i] - blockData[3+i];
                tmp1 = blockData[1+i] + blockData[7+i];
                tmp2 = blockData[3+i] + blockData[5+i];
                b6 = blockData[1+i] - blockData[7+i];
                b7 = tmp1 + tmp2;
                m0 =  blockData[0+i];
                x4 =  ((b6*473 - b4*196 + 128) >> 8) - b7;
                x0 =  x4 - (((tmp1 - tmp2)*362 + 128) >> 8);
                x1 =  m0 - b1;
                x2 =  (((blockData[2+i] - blockData[6+i])*362 + 128) >> 8) - b3;
                x3 =  m0 + b1;
                y3 =  x1 + x2;
                y4 =  x3 + b3;
                y5 =  x1 - x2;
                y6 =  x3 - b3;
                y7 = -x0 - ((b4*473 + b6*196 + 128) >> 8);

                blockData[0+i] =  (b7 + y4 + 128) >> 8;
                blockData[1+i] =  (x4 + y3 + 128) >> 8;
                blockData[2+i] =  (y5 - x0 + 128) >> 8;
                blockData[3+i] =  (y6 - y7 + 128) >> 8;
                blockData[4+i] =  (y6 + y7 + 128) >> 8;
                blockData[5+i] =  (x0 + y5 + 128) >> 8;
                blockData[6+i] =  (y3 - x4 + 128) >> 8;
                blockData[7+i] =  (y4 - b7 + 128) >> 8;
            }
        }

        public void Play()
        {
            if(isPlaying)
                return;

            isPlaying = true;
            NextFrameScheduler();
        }

        public void Pause()
        {
            isPlaying = false;
        }

        public void Stop()
        {
            _reader.Index = firstSequenceHeader;
            Pause();
        }

        public void NextFrame() {
            int code = _reader.FindNextMPEGStartCode();

            switch(code)
            {   
                case CrapPEGConstants.START_SEQUENCE:
                    DecodeSequenceHeader();
                break;
                case CrapPEGConstants.START_PICTURE:
                    if(isPlaying) 
                        NextFrameScheduler();
                    
                    DecodePicture();
                break;
                case CrapPEGConstants.NOT_FOUND:
                    Stop();

                    if(isLooping && sequenceStarted)
                        Play();
                break;
                default:
                    // Ignore.
                break;
            }

        }

        public void NextFrameScheduler()
        {
            double stallTime = (1000 / frameRate) - lateTime; 
            targetTime = DateTime.Now.Millisecond + stallTime; 

            if(stallTime < 18) // if the stalltime is less than 18 (milliseconds iirc) we go to the next time
                NextFrame();
            else {
                
            }
                
        }
    }
}
