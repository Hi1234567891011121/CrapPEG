/*
   BudgetMPEGSharp by ErieVS, github.com/erievs

   I would consider this to be under MIT license. It is largely based on 
   old commit of phoboslab's https://github.com/phoboslab/jsmpeg/ which itself
   is based largely based an an Open Source Decoder for Java under GPL, 
   and jsmpeg I guess also looked through another Decoder from Nokia 
   ,which the say is under no particular license, for certain aspects.
   
   This is based on the commit '42d572060835582aae4f59292d5a29ac09eece08', as I just
   wanted to get something to play video for now. It is a lot more compilcated in
   future versions of this.

   I'm not sure if this work is "derivative" enough to have a different license
   but then again, as the author of JSMpeg, says, who still cares about MPEG1?
   This is more for fun than any real-world use cases; it happened that 
   jsmpeg is the eaiest of the decoders for me to understand and base around.

   Just a word of warning, this project is kind of desiged janky as all hell, I mean
   this is a static class and all, for example there is the centeral static CurrentData
   which means I have to have an Intaltize method that you call after setting a path to CurrentData
   otherwise the BitReader won't get the proper path. I very much CAN redesign it to be better, maybe
   not static at some point. But this is more or less a proof of conecpt and such.

   In comments, I said 'section' a lot, the better word would be sequence to my knoweldge as that 
   is what they're officaly called in MPEG? So if you are confused what 'sequence' and 'section' are
   they're the same. They're just the indivual parts that encode/make up the video and such. As far as
   remember there is a I-Frame followed by P-Frames in each sections, there's more to it but you'll
   get it as you read the source code.

   All the current data is accessed through CurrentData, and that's how you get details on the current
   sequence and frame and what not, colours and stuff to use for your project. I know this is a weird
   and crappy way to do it, but I wrote the baises to this project with only a few hours of sleep over the course
   of a week. I have had much better sleep, so that's good, this week. I didn't think about this (the latecny, and size of tranport 
   of data would probbaly make this not super viable) but you maybe to transit the current data, and do streaming 
   or something. Not really intended, but maybe?

   If you are using this as a reference for another project. I'd use VLC to verify if you're reading data properly
   rather than 'ffmpeg -i', for some reason ffmpeg reads mpeg 1 codec info differently than VLC, like framerate. VLC
   will match with the data read by this, but not 'ffmpeg -i', I figure that VLC is right (and this) because it is playing, use
   the codec info and media info in VLC.

   You will need an external update loop for NextFrame(), I realized that using a while loop in this libary is a pretty
   a pretty bad idea! Since well it'll eat up the base thread and stalls raylib untuil that while loop is over, you could
   always put it on another thread. But just having it where you need to have an update loop in whereever you're using this is 
   eaiser. It just needs a while(true) loop, and the timings and everything are handled here (we use date time and such)

   My code style is weird I know, for me I use camel case for private varibles and normal public varible, but for public static varibles
   with gesetters I use captial letters. I don't know why it just feels right to me, also I use CaptialCamel for methods even though
   I guess officaly you are supposed to use camelCase, but it's my code and I'll do what I like. 

   I may have confused Luma and Chroma in the comments in some parts, if the code says luma but i say chroma
   and vice-a-versa in the comments, trust the code. I think I may have meant luma when I said chroma. Just a
   warning.

   The first release of this may or may not include b-frame support, I am working on getting it to feature parity first to that old branch of 
   JSMPEG and then probbaly release it; the branch I based this off of didn't include b-frame support, however depending on how much longer I
   want to spend on this, I'll implemte them. If it takes no time at all, it'll probbaly launch with b-frame support and I'll cross this off, but idk.
   
   There will probbaly no D frame support ever, because they were removed in MPEG-2, they had VERY limited usecases, and really were not used
   all that much, esspicaly later on. 
    
   Audio (MP2) is not supported (FOR NOW), I will try to implemte audio support at somepoint, but I honestly have no idea how to output the sound properly, compared
   to video. The decoding is fairly simple compared to video BUT outputing that sound is harder.

   I may port this to the programming language D at some point

   To Do:
   - B Frame (if not already added)
   - Audio (MP2) 

   Based on "JSMpeg" by Photolabs:
   https://github.com/phoboslab/jsmpeg/ (This Branch: https://github.com/phoboslab/jsmpeg/blob/42d572060835582aae4f59292d5a29ac09eece08/jsmpg.js#L1666)

   Based (By Proxy) on "Java MPEG-1 Video Decoder and Player" by Korandi Zoltan:
   http://sourceforge.net/projects/javampeg1video/
   
   Inspired by "MPEG Decoder in Java ME" by Nokia:
   http://www.developer.nokia.com/Community/Wiki/MPEG_decoder_in_Java_ME

   This Is So Helpful:
   http://dvdnav.mplayerhq.hu/dvdinfo/mpeghdrs.html

   I Keep Cutting The Logging Of Current Data; Forget What I was Doing; Forget How To Seralize Objects Prettly; And I Have To Spend 5 Minutes Looking For The Stackoverflow Article; So I Am Just Going To Keep It Here
   https://stackoverflow.com/questions/65620631/how-to-pretty-print-using-system-text-json-for-unknown-object

   This Is Very Helpful:
   https://cs.stanford.edu/people/eroberts/courses/soco/projects/data-compression/lossy/jpeg/dct.htm

   This Here Is Also Very Helpful:
   https://medium.com/@chawthirisan/spatial-vs-frequency-domain-a-guide-to-image-interpretation-d9c16b129b3f
*/

using System.Diagnostics;
using System.Runtime.Intrinsics.Arm;
using System.Text.Json;
using Raylib_cs;

namespace BudgetMPEGSharp.Libs
{
    public static class BudgetMPEGSharpDecoder
    {
        // this will be (hopefully) used to get data from the libary to the display
        public static BudgetMPEGData CurrentData = new BudgetMPEGData();
        public static BudgetMPEGBitReader? BitReader = null;

        private static long debugFrameCounter = 0; // this is just used for logging for devolpment, we can check every second by doing (debugFrameCounter % frameRate) you will have to probbaly check >= 0.05 or something because some framerates are a clean whole number but are instead like 29.97

        public static int ReadCode(byte[] codeTable)
        {
            int state = 0;

            do {
                int bit = BitReader.GetBits(1);

                if(bit == BudgetMPEGTables.END_OF_FILE)
                    return BudgetMPEGTables.END_OF_FILE;

                state = codeTable[state + bit];
            } while(state >= 0 && codeTable[state] != 0 );
            
            return codeTable[state + 2];
        }  

        public static int ReadCode(short[] codeTable)
        {
            var state = 0;
            do {
                state = codeTable[state + BitReader.GetBits(1)];
            } while( state >= 0 && codeTable[state] != 0 );
            return codeTable[state+2];
        }  

        public static int ReadCode(int[] codeTable)
        {
            var state = 0;
            do {
                int bits = BitReader.GetBits(1); 

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
                if(bits == BudgetMPEGTables.END_OF_FILE)
                {
                    
                }


                state = codeTable[state + bits];
            } while( state >= 0 && codeTable[state] != 0 );
            return codeTable[state+2];
        }  

        public static int FindStartCode(int code)
        {
            int current = 0;
            while( true ) {
                current = BitReader.FindNextMPEGStartCode();
                if( current == code || current == BudgetMPEGTables.NOT_FOUND) {
                    return current;
                }
            }
        }

        public static void FillArray(byte[] a, byte value)
        {
            for(int i = 0, length = a.Length; i < length; i++ ) {
                a[i] = value;
            }
        }

        public static void FillArray(int[] a, int value)
        {
            for(int i = 0, length = a.Length; i < length; i++ ) {
                a[i] = value;
            }
        }

        public static void FillArray(BudgetMPEGColour[] a, BudgetMPEGColour value)
        {
            for(int i = 0, length = a.Length; i < length; i++ ) {
                a[i] = value;
            }
        }

        public static BudgetMPEGColour[,] FillArray(BudgetMPEGColour[,] a, BudgetMPEGColour value)
        {
            BudgetMPEGColour[,] output = new BudgetMPEGColour[a.GetLength(0),  a.GetLength(1)];

            for(int y = 0; y < a.GetLength(0); y++) {
                for(int x = 0; x < a.GetLength(1); x++)
                {
                    output[y,x] = value;
                }
            }

            return output;
        }


        public static void Intaltize()
        {
            BitReader = new BudgetMPEGBitReader();
            
            /*
                It took me a bit to really have it click. But all we really need to care about
                is each 'section' of 'MPEG'. We find the start, we get it's header and we do stuff,
                I spent too long thinking about it as a whole picure, that there is only one width,
                and height. Nope, we are just looking at blocks, and doing stuff. This is kind of confusing
                wording and all (I have ADHD ramblings that only I get), but you'll get it.
            */
            
            FindStartCode(BudgetMPEGTables.START_SEQUENCE); // get the first section
            CurrentData.FirstSequenceHeader = BitReader.Index; // we want to store where the first header is for going back and such
            DecodeSequenceHeader();

            NextFrame();    
        }

        public static void DecodeSequenceHeader()
        {
            /*
                Basicaly just tells us the details of each section (or sequence as is proper).

                - The first 24 bits (first 12 is width, and second half is height) is Width/Height.
                - The 4 bits after are the pixel aspect ratio (we aint gonna use em but whatever).
                - The 4 bits afters are the framerate (they're is only 16(ish) supported ones, we have em defined in BudgetMPEGTables)
                - The 18 bits after the bitRate (we are not really going to need it, but I like reading it anyways)
                - The 1 bit after that is the marker (we aint gonna really need this)
                - The 10 bits after are bufferSize (we aint gonna really need this as well)
                - The 1 bit after this is the (constrained we don't really need it)

                Some values are 4 bits! So some have table that define the value (aspect ratio and frame rate)!
                I'd imagine this is done to save some space, and since this codec was designed for VCDs so ain't
                too many aspect ratios really. 

                Use VLC to verify properties, FFMPEG reads the framerate wrong. VLC is fine, FFMPEG seems like it reads 
                everything else fine though.

                A sequence just contains metadata, groups of pictures, and what not. They make up the entire video, it is not
                just one frame it is just a collect of frames and such, there is always an I-Frame (iirc) and in each section of a group of 
                pictures, with smaller differnt types of frame that rely on that i-Frame (we can store less data if we base on the IFrame). They make up
                a video. I also call em sections because why not.

                Sequences are made up of multiple stuff, different types of frames, and all. They can very in size, some are larger and smaller,
                but they just divy up the video.

                You will have to call advance() when skipping bits, because we are reading from the index
                and if say you don't wanna read the bitRate, but wanna read the marker we must advance 18.
            */

            CurrentData.Width =  BitReader.GetBits(12); // we MUST read width first (even though I like height before width)
	        CurrentData.Height = BitReader.GetBits(12);

            CurrentData.AspectRatio = BudgetMPEGTables.ASPECT_RATIO[BitReader.GetBits(4)]; // get the aspect ratio from table.
            CurrentData.Framerate = BudgetMPEGTables.FRAME_RATE[BitReader.GetBits(4)]; // get the framerate from the table
            
            BitReader.AdvanceBits(30); // skip a bunch
            
            // i have it set by default to these tables in the CurrentData, but we (probbaly) have to set it back to default, but the default only applies when intaited, we are setting this later on so we must set it to default again (I more of just wanted to avoid a null in currentdata) these are used to get the orginal value (we qaunted it so it takes up less space, compression) i love yapping as you can tell just read http://dvdnav.mplayerhq.hu/dvdinfo/mpeghdrs.html
            CurrentData.IntraQuantMatrix = BudgetMPEGTables.DEFAULT_INTRA_QUANT_MATRIX; // the qaunt matrix for the 'full' real picture (intra)
            CurrentData.NonIntraQuantMatrix = BudgetMPEGTables.DEFAULT_NON_INTRA_QUANT_MATRIX; // the qaunt for the frames and such (inter, or in between as the name suggest) the full normal intra frames 
    
            // we must get the width/height of a macroblock; see (some) frames are divided into marcoblocks; macroblocks are divided into blocks - 4 lumance - 1 cb - 1 cr; as far as i am away full frame (i frame) picture has all of these macroblocks; other types of frame only store difference for certain macroblocks like motion
            CurrentData.MacroBlockHeight = (CurrentData.Height + 15) >> 4;
            CurrentData.MacroBlockWidth = (CurrentData.Width + 15) >> 4;
            CurrentData.MacroBlockSize = CurrentData.MacroBlockHeight * CurrentData.MacroBlockWidth; // just how big it needs to be 

            // this is the size before we qaunt/decode macroblocks, so it is smaller of course because we compressed, so it should be smaller
            CurrentData.CodedHeight = CurrentData.MacroBlockHeight << 4;
            CurrentData.CodedWidth = CurrentData.MacroBlockWidth << 4;
            CurrentData.CodedSize = CurrentData.CodedHeight * CurrentData.CodedWidth; // again just how big it needs to be

            // we need the half with and height because images use something called chorma subsamping; the launamnce is a block of 4, and the cB and Ce are 2, so it'd be half of laumance and the size of each would be a qauter
            CurrentData.HalfWidth = CurrentData.MacroBlockWidth << 3;
            CurrentData.HalfHeight = CurrentData.MacroBlockHeight << 3;
            CurrentData.QuarterSize = CurrentData.CodedSize >> 2; 

            // here we check if we need to load an a custom intra qaunt matrix or nah
            if(BitReader.GetBits(1) == 1) // while GetBits returns a intger, this is just a bool here, 1 is true 0 is fall, on off, etc
            {
                for(int i = 0; i < BudgetMPEGTables.QUANT_MATRIX_LENGTH; i++)
                {
                    CurrentData.CustomIntraQuantMatrix[BudgetMPEGTables.ZIG_ZAG[i]] = (byte) BitReader.GetBits(8); // honestly I don't know if we really need to load it into a CustomIntraQuantMatrix, we may be able to just load it into IntraQuantMatrix, but I guess a just in case
                }

                CurrentData.IntraQuantMatrix = CurrentData.CustomIntraQuantMatrix;
            }

            // here we check if we need to load an a custom NON intra qaunt matrix or nah
            if(BitReader.GetBits(1) == 1) // while GetBits returns a intger, this is just a bool here, 1 is true 0 is fall, on off, etc
            {
                for(int i = 0; i < BudgetMPEGTables.QUANT_MATRIX_LENGTH; i++)
                {
                    CurrentData.CustomNonIntraQuantMatrix[BudgetMPEGTables.ZIG_ZAG[i]] = (byte) BitReader.GetBits(8); // honestly I don't know if we really need to load it into a CustomNonIntraQuantMatrix, we may be able to just load it into NonIntraQuantMatrix, but I guess a just in case
                }

                CurrentData.NonIntraQuantMatrix = CurrentData.CustomNonIntraQuantMatrix;
            }

            // we don't have to set buffers and what not if we already started the sequence
            if(CurrentData.SequenceStarted) { return; }
	            CurrentData.SequenceStarted = true;

            // get the colours for the current frame
            CurrentData.CurrentY = new byte[CurrentData.CodedSize];
            CurrentData.CurrentCr = new byte[CurrentData.CodedSize >> 2]; 
            CurrentData.CurrentCb = new byte[CurrentData.CodedSize >> 2];

            // get the foward colours, the colours for montion compensation, that type of frame, we are storing the colours that change and all
            CurrentData.FowardY = new byte[CurrentData.CodedSize];
            CurrentData.FowardCr = new byte[CurrentData.CodedSize >> 2]; 
            CurrentData.FowardCb = new byte[CurrentData.CodedSize >> 2];

            // this really all the end user of the libary (should) need to use, it is the all the colours of frame you need to display, the active frame or whatever, you can display it however you like just read CurrentData. 
            CurrentData.FrameBuffer = new BudgetMPEGColour[CurrentData.Height * CurrentData.Width]; // i used a custom struct since all we need is RGB not all the stuff from drawing 
            FillArray(CurrentData.FrameBuffer, new BudgetMPEGColour(255, 255, 255));
        }

        public static void DecodePicture()
        {
            /*
                Just like for Sequences, Pictures have a header BUT it is MUCH smaller (i think 4 bits all togather, but the last three aint used here).

                - The first 10 bits are the temperal sequence number, we don't NEED em or anything for decodong, but I like to read it for fun
                - The next 3 bits are the frame type (they're 4), there's I,P,B,D (I's are pretty much just jpegs, d is something weird and iirc is a low res d used VERY seldomly on very old hardware for faster seeking previous, not found in MPEG 2 or future versions, P and B frames are frames based on the I frame and aren't full pictures, the store difference (in the simpliest explation I can write, you'll see more how we decode and everything) between the I frame and the current frame I frames perdict in one direction while B frames do it in two directiomns hence its name bi (yay!) so it bases off of a backwards and fowards frame instead ojust one )
                - The next 16 bits are the VBV delay, same case as the 10 bits where we don't really need it for decoding or anything, but I just like logging it
                - That concludes the picture header, the bits following the VBV are the real meat and (I cannot spell poatato, I am like Dan Quale here) of data
            */
            CurrentData.TempernalSequenceNumber = BitReader.GetBits(10); // we don't need it but honestly I just like reading as much data for the helll of it, it ain't used in decoding the image or anything, don't even know if it is really commonly encoded into these files but eh not too diffucilt to implement
            CurrentData.PictureCodingType = (byte) BitReader.GetBits(3); // we NEED this, this tells us the type of frame, I went into detail in the comment above, (1 is I, 2 is P, 3 is B, and 4 is D), we have to cast byte because we retrun the value of bits as an int for convence but I store it as a byte because it is the smallest dataty[e (that is easiy to do we could I guess do weird things with bools but way too much for for little or no gain) to use 4 bits (if that would even work) but just storing it in a byte works fine]
            CurrentData.VBVDelay = BitReader.GetBits(16); // this is the same case as the TempernalSequenceNumber where I log for the hell of it rather than a real use case 
      
            if(CurrentData.PictureCodingType <= BudgetMPEGTables.PICTURE_TYPE_NULL 
                || CurrentData.PictureCodingType >= BudgetMPEGTables.PICTURE_TYPE_B) // for now we don't have b-frame support (this will of course be removed if we do), and that's a 3, and D frames are 4 (we will never have d-frame probbaly so i'll replace this with just a > if/when i hopefully add b frame support)
                return;

            // we must get pelFoward code and f codes and what if we are a P type
            if(CurrentData.PictureCodingType == BudgetMPEGTables.PICTURE_TYPE_P)
            {
                CurrentData.FullPixelFoward = Convert.ToBoolean(BitReader.GetBits(1)); // opefuly this works single line conversions of bits to bools confuse me A LOT
                CurrentData.FowardFCode = BitReader.GetBits(3);

                if(CurrentData.FowardFCode == 0)
                    return;

                CurrentData.ForwardRSize = CurrentData.ForwardRSize - 1;
                CurrentData.FowardF = 1 << CurrentData.ForwardRSize;
            }

            int code;
            do
            {
                code = BitReader.FindNextMPEGStartCode();
            } while (code == BudgetMPEGTables.START_EXTENSION || code == BudgetMPEGTables.START_USER_DATA);
        
            while(code >= BudgetMPEGTables.START_SLICE_FIRST && code <= BudgetMPEGTables.START_SLICE_LAST)
            {
                DecodeSlice(code & 0x000000FF);
                code = BitReader.FindNextMPEGStartCode();
            }

            // we found start code then
            BitReader.RewindBits(32);

            YCbCrToRGB(); // load/set colours

            // check if we have a reference picutre (I or P they refence each other)
            if(CurrentData.PictureCodingType == BudgetMPEGTables.PICTURE_TYPE_I 
                || CurrentData.PictureCodingType == BudgetMPEGTables.PICTURE_TYPE_P) {
                
                byte[] tempY = CurrentData.FowardY;
                byte[] tempCr = CurrentData.FowardCr;
                byte[] tempCb = CurrentData.FowardCb;

                CurrentData.FowardY = CurrentData.CurrentY;
                CurrentData.FowardCr = CurrentData.CurrentCr;
                CurrentData.FowardCb = CurrentData.CurrentCb;

                CurrentData.CurrentY = tempY;
                CurrentData.CurrentCb = tempCb;
                
                CurrentData.CurrentCr = tempCr;
            }
        }

        public static void YCbCrToRGB()
        {
            int width = CurrentData.Width;
            int height = CurrentData.Height;
            
            int yIndex = 0;
            int cIndex = 0;

            byte[] pY = CurrentData.CurrentY;
            byte[] pCr = CurrentData.CurrentCr;
            byte[] pCb = CurrentData.CurrentCb;

            byte r = 255, g = 255, b = 255;
            byte cy, cr = 128, cb = 128;

            for (int i = 0; i < width * height; i++) {
                
                cy = pY[yIndex++];
       

                r = (byte)(cr + ((cr * 103) >> 8) - 179);
                g = (byte)(((cb * 88) >> 8) - 44 + ((cr * 183) >> 8) - 91);
                b = (byte)(cb + ((cb * 198) >> 8) - 227);
                
                CurrentData.FrameBuffer[i] = new BudgetMPEGColour((byte)(cy + r), (byte)(cy - g), (byte)(cy + b));
            }
        }


        public static void DecodeSlice(int slice)
        {
            CurrentData.SliceBegin = true;
            CurrentData.MacroblockAddress = (slice - 1) * CurrentData.MacroBlockWidth - 1;

            CurrentData.MotionFowardH = CurrentData.MotionPreviousH = 0;
            CurrentData.MotionFowardV = CurrentData.MotionPreviousV = 0;

            CurrentData.DCPredictorY = 128;
            CurrentData.DCPredictorCr = 128;
            CurrentData.DCPredictorCb = 128;

            CurrentData.QuantizerScale = BitReader.GetBits(5); // get the scale from 5 bits thingy

            while(BitReader.GetBits(1) == 1)
                BitReader.AdvanceBits(8);
            
            do 
		        DecodeMacroblock();
            while (!BitReader.NextBytesAreStartCode());
        }

        public static void DecodeMacroblock() // what images are divided into (IIRC) and we split images into and what not
        {
            int increment = 0;
            int target = ReadCode(BudgetMPEGTables.MACROBLOCK_ADDRESS_INCREMENT); // read the target from the array thing (matrix is the legal name i think)

            while(target == 34)
                target = ReadCode(BudgetMPEGTables.MACROBLOCK_ADDRESS_INCREMENT); // macroblock_stuffing
            
            while(target == 35) { // macroblock_escape
                increment += 33;
                target = ReadCode(BudgetMPEGTables.MACROBLOCK_ADDRESS_INCREMENT);   
            }

            increment += target;
            
            // we must process any skipped blocks
            if(CurrentData.SliceBegin)
            {
                CurrentData.SliceBegin = false;
                CurrentData.MacroblockAddress += increment;
            } else { // if in slice not at begining do stuff
                if(CurrentData.MacroblockAddress + increment >= CurrentData.MacroBlockSize) // check if too large and return if so it cannot be TOOO or bad stuff happens
                    return;

                if(increment > 1)
                {
                    // skipped macroblocks reset DC predictors
                    CurrentData.DCPredictorY  = 128;
                    CurrentData.DCPredictorCr = 128;
                    CurrentData.DCPredictorCb = 128;
                    
                    // sipped macroblocks in p pictures reset their motion vectors
                    if(CurrentData.PictureCodingType ==  BudgetMPEGTables.PICTURE_TYPE_P) {
                        CurrentData.MotionFowardH = CurrentData.MotionPreviousH = 0; // reset 
                        CurrentData.MotionFowardV = CurrentData.MotionPreviousH = 0; // reset
                    }
                }

                // we must now perdict the skipped macroblocks and copy stuff to
                while(increment > 1) {
                    CurrentData.MacroblockAddress++;
                    CurrentData.MacroblockRow = (CurrentData.MacroblockAddress / CurrentData.MacroBlockWidth) | 0;
                    CurrentData.MacroblockCol = CurrentData.MacroblockAddress % CurrentData.MacroBlockWidth;

                    CopyMacroblock(CurrentData.MotionFowardH, CurrentData.MotionFowardV, CurrentData.FowardY, CurrentData.FowardCr, CurrentData.FowardCb);
                    increment--;
                }

                CurrentData.MacroblockAddress++;
            }

            CurrentData.MacroblockRow = (CurrentData.MacroblockAddress / CurrentData.MacroBlockWidth) | 0;
            CurrentData.MacroblockCol = CurrentData.MacroblockAddress % CurrentData.MacroBlockWidth;

            // we must now process the current macroblock
            CurrentData.MacroblockType = ReadCode(BudgetMPEGTables.MACROBLOCK_TYPE_TABLES[CurrentData.PictureCodingType]);   
            CurrentData.MacroblockIntra = (CurrentData.MacroblockType & 0x01) != 0; // I think think should work I think if the result is 1 then true, and the inverse is true, should be but also I am stupid?
            CurrentData.MacroblockMotionFoward = (CurrentData.MacroblockAddress & 0x08) != 0; // I think it should work here same idea idk (iirc it should but also I miss many things or miss interupt and what not and yap as i am doing now and get distracted)
        
            // we need to get the qaunt scale
            if((CurrentData.MacroblockType & 0x10) != 0)
                CurrentData.QuantizerScale = BitReader.GetBits(5);

            if(CurrentData.MacroblockIntra) { // if an intra frame (I frame), 'full' frame, not in between or based on another frame like inter frames
                // reset intra macroblocks motion vectores
                CurrentData.MotionFowardH = CurrentData.MotionPreviousH = 0;
                CurrentData.MotionFowardV = CurrentData.MotionPreviousV = 0;
            } else { // inter frame (B/P)
                CurrentData.DCPredictorY = 128;
                CurrentData.DCPredictorCr = 128;
                CurrentData.DCPredictorCb = 128;

                DecodeMotionVectors();
                CopyMacroblock(CurrentData.MotionFowardH, CurrentData.MotionFowardV, CurrentData.FowardY, CurrentData.FowardCr, CurrentData.FowardCb);
            }

            // now decode blocks (blocks make up macroblocks iirc)
            int cbpattern = ((CurrentData.MacroblockType & 0x02) != 0) ? ReadCode(BudgetMPEGTables.CODE_BLOCK_PATTERN) : (CurrentData.MacroblockIntra ? 0x3f : 0); // pattern
          
            for(int block = 0, mask = 0x20; block < 6; block++)
            {
                if((cbpattern & mask) != 0) // if the pattern & (and) mask is not 0  
                    DecodeBlock(block);

                mask >>= 1;
            }
        }

        public static void DecodeMotionVectors()
        {
            int code;
            int d = 0; // hopefully this won't mess anything up, looking through JSMPeg it doesn't set anything here? but we cannot not intlize here in C# with a value or we run into issues, i assume nothing abd will happen as we read the value and check if checking is needed but I guess if something breaks look here?
            int r = 0; 

            // for regular forward vectors
            if(CurrentData.MacroblockMotionFoward) { // check if motion foward
                
                // for horizontal foward vectors
                code = ReadCode(BudgetMPEGTables.MOTION);

                if((code != 0) && (CurrentData.FowardF != 1)) { // check if we need to read stuff for to get the proper d/r 
                    r = BitReader.GetBits(CurrentData.ForwardRSize); // get the r 
                    d = ((Math.Abs(code) - 1) << CurrentData.ForwardRSize) + r + 1;

                    if(code < 0) // if code is less than 0 turn d into neg
                        d = -d;
                } else { // if we don't need to read stuff
                    d = code;
                }
                CurrentData.MotionPreviousH += d;

                if(CurrentData.MotionPreviousH > (CurrentData.FowardF << 4) - 1)
                    CurrentData.MotionPreviousH -= CurrentData.FowardF << 5;
                else if(CurrentData.MotionPreviousH < ((-CurrentData.FowardF) << 4))
                    CurrentData.MotionPreviousH += CurrentData.FowardF << 5;

                CurrentData.MotionFowardH = CurrentData.MotionPreviousH;
                if(CurrentData.FullPixelFoward)
                    CurrentData.MotionFowardH <<= 1;

                // for verticle foward vectors
                code = ReadCode(BudgetMPEGTables.MOTION);

                if((code != 0) && (CurrentData.FowardF != 1)) { // check if we need to read stuff for to get the proper d/r (again)
                    r = BitReader.GetBits(CurrentData.ForwardRSize);
                    d = ((Math.Abs(code) - 1) << CurrentData.ForwardRSize) + r + 1;

                    if(code < 0) // if code is less than 0 turn d into neg
                        d = -d;
                } else { // if we don't need to read stuff
                    d = code;
                }

                CurrentData.MotionPreviousV += d;
                if(CurrentData.MotionPreviousV > (CurrentData.FowardF << 4) - 1)
                    CurrentData.MotionPreviousV -= CurrentData.FowardF << 5;
                else if(CurrentData.MotionPreviousV < ((-CurrentData.FowardF) << 4)) 
                    CurrentData.MotionPreviousV += CurrentData.FowardF << 5;

                CurrentData.MotionFowardV = CurrentData.MotionPreviousV;
                if(CurrentData.FullPixelFoward)
                    CurrentData.MotionFowardV <<= 1;
            } else if(CurrentData.PictureCodingType == BudgetMPEGTables.PICTURE_TYPE_P) { // (no info for motion) if not MacroblockMotionFoward and a pframe
                // no info - reset 
                CurrentData.MotionFowardH = CurrentData.MotionPreviousH = 0;
                CurrentData.MotionFowardV = CurrentData.MotionPreviousV = 0;
            }

        }

        public static void CopyMacroblock(int motionH, int motionV, byte[] sourceY, byte[] sourceCr, byte[] sourceCb) // copies a macroblock 
        {
            int width;
            int scan;

            int H;
            int V;

            bool oddH;
            bool oddV;

            int source;
            int destition; // i cannot spell very well oh well who cares
            int last;

            byte[] destitionY = CurrentData.CurrentY;
            byte[] destitionCb = CurrentData.CurrentCb;
            byte[] destitionCr = CurrentData.CurrentCr;

            // luminance
            width = CurrentData.CodedWidth; // full width, it is 4:2:0 sampling, so for lumaince each has full colour, BUT for 4 lumaince we only 1 colour iirc. :2 is 2x2 :2:2 is 2*1 (2x2 block share colour rather than just line in other sub samapling)
            scan = width - 16;

            H = motionH >> 1;
            V = motionV >> 1;

            oddH = (motionH & 1) == 1;
            oddV = (motionV & 1) == 1;

            source = ((CurrentData.MacroblockRow << 4) + V) * width + (CurrentData.MacroblockCol << 4) + H;
            destition = (CurrentData.MacroblockRow * width + CurrentData.MacroblockCol) << 4;
            last = destition + (width << 4);

            // this block gets confusing, I tried to format it, and comment it, but pay close attetion it is confusing (for me at least)
            if(oddH) // if oddH
            {
                // if also oddV
                if(oddV)
                {
                    while(destition < last)
                    {
                        for(int x = 0; x < 16; x++) { // full            
                //            destitionY[destition] = (byte)((sourceY[source] + sourceY[source + 1] + sourceY[source + width] + sourceY[source + width + 1] + 2) >> 2);
                           
                            destition++; 
                            source++;
                        }

                        destition += scan; 
                        source += scan;
                    }
                } else { // if not oddV
                    while(destition < last) {
                        
                        for( var x = 0; x < 16; x++ ) { // full
                           // sourceY[destition] = (byte)((sourceY[source] + sourceY[source + 1] + 1) >> 1);
                            destition++; 
                            source++;
                        }

                        destition += scan; 
                        source += scan;
                    }
                } 
            } else { // if not oddH

                if(oddV) { // if oddV is true
                    while(destition < last)
                    {
                        for(int x = 0; x < 16; x++) // full
                        {
                            //destitionY[destition] = (byte)((sourceY[source] + sourceY[source + width] + 1) >> 1);
                            
                            destition++; 
                            source++;
                        }

                        destition += scan;
                        source += scan;
                    }
                } else { // if oddV is NOT true
                    while(destition < last)
                    {
                        for(int x = 0; x < 16; x++) { // full
                         //   destitionY[destition] = sourceY[source];
                            
                            destition++; 
                            source++;
                        }

                        destition += scan;
                        source += scan;
                    }
                }
            }

            // chrominance
            width = CurrentData.HalfWidth; // chromoe is :2 (2x2) half width, for colour we store less colour info than lumanice (row cols)
            scan = width - 8;

            H = (motionH / 2) >> 1;
            V = (motionV / 2) >> 1;

            oddH = ((motionH/2) & 1) == 1;
	        oddV = ((motionV/2) & 1) == 1;

            source = ((CurrentData.MacroblockRow << 3) + V) * width + (CurrentData.MacroblockCol << 3) + H;
            destition = (CurrentData.MacroblockRow * width + CurrentData.MacroblockCol) << 3;
            last = destition + (width << 3);

            // another confusing block, very similar, I commented it again
            if(oddH) { // if oddH 
                if(oddV) // if also oddV
                {
                    while(destition < last)
                    {
                        for( var x = 0; x < 8; x++ ) { // half
                         //   destitionCr[destition] = (byte)((sourceCr[source] + sourceCr[source + 1] + sourceCr[source + width] + sourceCr[source + width + 1] + 2) >> 2);
                         //   destitionCb[destition] = (byte)((sourceCb[source] + sourceCb[source + 1] + sourceCb[source + width] + sourceCb[source + width + 1] + 2) >> 2);
                        
                            destition++; 
                            source++;
                        }

                        destition += scan;
                        source += scan;
                    }
                } else { // if NOT a oddV
                    while(destition < last) {
                        for( var x = 0; x < 8; x++ ) { // half 
                            //destitionCr[destition] = (byte)((sourceCr[source] + sourceCr[source + 1] + 1) >> 1);
                           // destitionCb[destition] = (byte)((sourceCb[source] + sourceCb[source + 1] + 1) >> 1);

                            destition++; 
                            source++;
                        }

                        destition += scan;
                        source += scan;
                    }
                }
            } else { // if not oddH
                if(oddV) // if an oddV 
                { 
                    while(destition < last) {
                        for(int x = 0; x < 8; x++) { // full
                           // destitionCr[destition] = (byte)((sourceCr[source] + sourceCr[source + width] + 1) >> 1);
                          //  destitionCb[destition] = (byte)((sourceCb[source] + sourceCb[source + width] + 1) >> 1);

                            destition++; 
                            source++;
                        }

                        destition += scan;
                        source += scan;
                    }
                } else { // if NOT an oddV
                    while(destition < last) {
                        for(int x = 0; x < 8; x++) { // full
                       //     destitionCr[source] = sourceCr[source];
                         //   destitionCb[source] = sourceCb[source];

                            destition++; 
                            source++; 
                        }

                        destition += scan;
                        source += scan;
                    }
                }
            }
        }

        public static void DecodeBlock(int block) // for decoding each block (which should be part of larger macroblocks hence its name macro)
        {
            int n = 0;
            byte[] qauntMatrix;
            
            // clear block data

            FillArray(CurrentData.BlockData, 0);

            // decode the DC coeffieceint of intra-code (they're very much like jpegs simailar maths and what not, aka i-frame well idk d frames maybe similar but idk they're not inter frames at least they don't rely on others) blocks if well intra
            if(CurrentData.MacroblockIntra) { 
                int predictor;
                int dctSize; // the size of the discrete cosine transform (DCT) basicly DCT just aranages data in a more efficent way with some math, we reverse it (look at the https://www.cmlab.csie.ntu.edu.tw/cml/dsp/training/coding/mpeg1/ for a real explantion)
            
                // the dc (aka discrete cosine) perdection, to my knowlege we are perdicting if it is a block of chromance or laumanice, also fun fact this is very similar to way jpegs do as a whole (not just this but the whole DCT iirc)
                if(block < 4) { // if a 4 block (should be luma)
                    predictor = CurrentData.DCPredictorY;
                    dctSize = ReadCode(BudgetMPEGTables.DCT_DC_SIZE_LUMINANCE); // read luma code LUMA why did I put chroma here lol  
                } else { // should do chroma stuff then instead
                    predictor = block == 4 ? CurrentData.DCPredictorCr : CurrentData.DCPredictorCb;
                    dctSize = ReadCode(BudgetMPEGTables.DCT_DC_SIZE_CHROMINANCE); // CHROMA i am so stupid did i do luma here lol 
                }

                // now read the DC coefficent (read more on that article if you care, but the DC coeffiecent is used for smaller quantization (reducing range), and AC for larger)
                if(dctSize > 0) {
                    int differential = BitReader.GetBits(dctSize);

                    if((differential & (1 << (dctSize - 1))) != 0) // this is for checking what to set 0 to and what we gotta do
                        CurrentData.BlockData[0] = predictor + differential; // set at the first part of data
                    else
                        CurrentData.BlockData[0] = predictor + ((-1 << dctSize) | (differential + 1)); 
                } else { // if it is less than or equal to zero the dct
                    CurrentData.BlockData[0] = predictor; // set the block to it, yeah I did the reverse for some reason and that caused issues
                }

                // save the value of the predictor
                if(block < 4) // detrime what to save so luma or chormo and what not the else if block will sort this out or something idk why I am writing like this  
                    CurrentData.DCPredictorY = CurrentData.BlockData[0];
                else if(block == 4) 
                    CurrentData.DCPredictorCr = CurrentData.BlockData[0];
                else 
                    CurrentData.DCPredictorCb = CurrentData.BlockData[0];

                CurrentData.BlockData[0] <<= 3 + 5;
                // deqaunt and pre multiplay or well un-reduce the numbers that the encoder of the videos we are reading reduced is probbay the correct wording 

                qauntMatrix = CurrentData.IntraQuantMatrix; // load the qaunt matrix
                n = 1;
            } else { // set the qauntMatrix to a non intra, as this is an inter (in between a  iframe full one)
                qauntMatrix = CurrentData.NonIntraQuantMatrix; // load the qaunt matrix as well here
            }

            // deocde AC (the larger I think) coefficients and DC for non intra frames (inter)
            int level = 0;
            while(true) {
                int run = 0;
                int coefficient = ReadCode(BudgetMPEGTables.DCT_COEFF); // load this table 

                // MY bad no wonder why it was unreachable you must break the while loop at some point lol
                if((coefficient == 0x0001) && (n > 0) && (BitReader.GetBits(1) == 0))
                    break;

                // ugggh issues realeating to 803 ish (zig zag int deZigZag)
                if(coefficient == 0xffff) { // if the coefficant is 0xffff
                    run = BitReader.GetBits(6); // get 6 bits
                    // escape it (i was tired of my lady so we planned our escape)
                    level = BitReader.GetBits(8);
                    // set level to stuff based on current level

                    if(level == 0) 
                        level = BitReader.GetBits(8);
                    else if(level == 128) 
                        level = BitReader.GetBits(8) - 256;
                    else if(level > 128)
                        level -= 256;
                } else { // if coefficient is different
                    run = coefficient >> 8;
                    level = coefficient & 0xff;

                    if(BitReader.GetBits(1) == 1) // check a flag (if we need to make neg)
                        level = -level;
                }

         

                n += run;
                n = Math.Clamp(n, 0, 63);
          
                int deZigZagged = BudgetMPEGTables.ZIG_ZAG[n]; // (I had fun times debugging this one, overflow, n would be a few values over length, and I had issues finding out why, annoying as all heck, but it just caused by a bunch of typos here chroma/luma flipping, etc ) the encoder also 'zig zags' scans the values from AC, so we just gotta also unzig zag them now
                n++;

                // deqaunt and stuff, oddify, clip, and what not
                level <<= 1;
                if(!CurrentData.MacroblockIntra) // if an inter frame (so not full image just stuff based on the image)
                    level += (level < 0 ? -1 : 1);
                
                level = (level * CurrentData.QuantizerScale * qauntMatrix[deZigZagged]) >> 4; // set lebel based on the matrix and what not
                if((level & 1) == 0)
                    level -= level > 0 ? 1 : -1;

                if(level > 2047) // if overflow
                    level = 2047;
                else if(level < -2048) // if underflow
                    level = -2048;

                // save now to block data 
                CurrentData.BlockData[deZigZagged] = level * BudgetMPEGTables.PREMULTIPLIER_MATRIX[deZigZagged]; // use the premul matrix and such and store
            }

            // tranforms the block data to little spatial domain thingy
            if(n == 1) // only DC, not IDCT IS NEEDED yay!
                FillArray(CurrentData.BlockData, (CurrentData.BlockData[0] + 128) >> 8);
            else   
                InverseDiscreteCosineTransformation();

            // move the block TO ITS PLACE hella yeah
            byte[] destitionArray;
            int destitionIndex;
            int scan;

            if(block < 4) {// do stuff slightly differntly based on size of block (we use that to detimre chroma or luma, luma is less than 4, as we store all the luma, but we store one chroma for 4 luma so if it is 4 or more it is chroma colour 4:2:0)
                destitionArray = CurrentData.CurrentY;
                scan = CurrentData.CodedWidth - 8;
                destitionIndex = (CurrentData.MacroblockRow * CurrentData.CodedWidth + CurrentData.MacroblockCol) << 4;

                if((block & 1) != 0)
                    destitionIndex += 8;
                if((block & 2) != 0)
                    destitionIndex += CurrentData.CodedWidth << 3;
            } else { // less crap
                destitionArray = (block == 4) ? CurrentData.CurrentCb : CurrentData.CurrentCr;
                scan = (CurrentData.CodedWidth >> 1) - 8;
                destitionIndex = ((CurrentData.MacroblockRow * CurrentData.CodedWidth) << 2) + (CurrentData.MacroblockCol << 3);
            }

            n = 0; // reset n
            
            if(CurrentData.MacroblockIntra) // if intra
            {
                int mult = 0;

                // overwrite, we aint perdicting in i-frames aka intra frames, as this is the baisies for our future perductions!
                for(int row = 0; row < 8; row++) // loop through rows (y)
                {
                    for(int col = 0; col < 8; col++) // loop through colums (x)
                    {
                        destitionArray[destitionIndex++] = (byte) CurrentData.BlockData[n++]; // hopefully the cast will make it happy we we won't run into issues (did I just jinx it?)
                    }

                    destitionIndex += scan;
                }
            } else { // if inter (or well not intra)
                // add to the predicted macroblock (very similar to the other line the only different is adding, I guess we could do it in one, but I imagine checking the intra once and then doing the code is bit more efficent that checking every loop, esspicaly as we are ran in a loop)
                for(int row = 0; row < 8; row++) // loop through rows (y)
                {
                    for(int col = 0; col < 8; col++) // loop through colums (x)
                    {
                        destitionArray[destitionIndex++] += (byte) CurrentData.BlockData[n++]; // hopefully the cast will make it happy we we won't run into issues (did I just jinx it?)
                    }

                    destitionIndex += scan;
                }
            }
        }   

        public static void InverseDiscreteCosineTransformation() // reconstrauct values
        {
            /*
                This is honestly much simipiler than honestly it looks (it took me a bit for it to click). 

                The goal of this is just to do the reverse what the encoder did. The encoder uses an operation
                known as a discrete cosine transform which takes 'spatial domain', the raw brightness/colour info (YCrCB),
                and transforms (hence its name) it into 'frequency domain', storing info as represented by its frequency 
                components (how values change).

                The big benfit of transforming into frequency domain is that low frequencies tend to be near the centre,
                and higher frequencies foward. We can remove a lot of the lower frequencies, as we we cannot really them
                all that much. This can save A LOT of space, esspicaly over the span of a long video.

                See Here:
                https://medium.com/@chawthirisan/spatial-vs-frequency-domain-a-guide-to-image-interpretation-d9c16b129b3f 

                See Here:
                https://cs.stanford.edu/people/eroberts/courses/soco/projects/data-compression/lossy/jpeg/dct.htm

                Now, for a decoder, we simply just have to reverse the operation. Take the frequencies data and turn it back into
                saptial data, data we can easily display.

                It looks fairly complicated, because they're a far number of steps involved, esspicaly as we are doing 8x8 blocks
                , I tried formating it all nicely, and what not. But you can get lost in the sauce, because they're's a lot 
                of math in indexs acess with numbers that are close. 

                Now the operation involves a good deal of steps and such, not HARD steps easy, but just a bunch. It took me a while
                because I kept making minor typos, which caused System.IndexOutOfRangeException:, fun times. Just go around and fix em
                if you are using this as a refrence for another project and you have the same issue.
                
                But just look carefuly, and you'll get it to a degree, maybe not to a degree that you can do it on paper
                , but enough where you get the idea, and understand. 
            */
            int index;
    
            int base1; // I know you can do int test1, test2, etc but I typicaly don't like doing them in method because to me it gets heard to understand
            int base2;
            int base3;
            int base4;
            int base5;
            int base6;
            int base7;

            int temp1;
            int temp2;

            int move0;

            int expended0;
            int expended1;
            int expended2;
            int expended3;
            int expended4;

            int returned3;
            int returned4;
            int returned5;
            int returned6;
            int returned7;


            // transform the columes using math and values and what not
            for(index = 0; index < 8; ++index) { // (decend) all colour is in 8 because 4 seprete luma 2 2 for colour make for 8 
                // transform
                base1 = CurrentData.BlockData[4 * 8 + index]; // remember each block is 0-7 a total of 8
                base3 = CurrentData.BlockData[2 * 8 + index] + CurrentData.BlockData[6 * 8 + index];
                base4 = CurrentData.BlockData[5 * 8 + index] - CurrentData.BlockData[3 * 8 + index];
                
                temp1 = CurrentData.BlockData[1 * 8 + index] + CurrentData.BlockData[7 * 8 + index];
                temp2 = CurrentData.BlockData[3 * 8 + index] + CurrentData.BlockData[5 * 8 + index];

                base6 = CurrentData.BlockData[1 * 8 + index] + CurrentData.BlockData[7 * 8 + index];
                base7 = temp1 + temp2;

                move0 = CurrentData.BlockData[0 * 8 + index];
                
                expended4 = ((base6 * 473 - base4 * 196 + 128) >> 8) - base7;
                expended0 = expended4 - (((temp1 - temp2) * 362 + 128) >> 8);
                expended1 = move0 - base1;
                expended2 = (((CurrentData.BlockData[2 * 8 + index] - CurrentData.BlockData[6 * 8 + index]) * 362 + 128) >> 8) - base3;
                expended3 = move0 + base1;

                returned3 = expended1 + expended2;
                returned4 = expended3 + base3;
                returned5 = expended1 - expended2;
                returned6 = expended3 - base3;
                returned7 = -expended0 - ((base4 * 473 + base6 * 196 + 128) >> 8);

                // store
                CurrentData.BlockData[0 * 8 + index] = base7 + returned4;
                CurrentData.BlockData[1 * 8 + index] = expended4 + returned3;
                CurrentData.BlockData[2 * 8 + index] = returned5 - expended0;
                CurrentData.BlockData[3 * 8 + index] = returned6 - returned7;
                CurrentData.BlockData[4 * 8 + index] = returned6 + returned7;
                CurrentData.BlockData[5 * 8 + index] = expended0 + returned5;
                CurrentData.BlockData[6 * 8 + index] = returned3 - expended4;
                CurrentData.BlockData[7 * 8 + index] = returned4 - base7;
            }

            // transform the rows using math and values and what not 
            for(index = 0; index < 64; index += 8) { // increaments of 8, because each block is 8x8 this is row
                // transform
                base1 = CurrentData.BlockData[4 + index];
                base3 = CurrentData.BlockData[2 + index] + CurrentData.BlockData[6 + index];
                base4 = CurrentData.BlockData[5 + index] - CurrentData.BlockData[3 + index];

                temp1 = CurrentData.BlockData[1 + index] + CurrentData.BlockData[7 + index];
                temp2 = CurrentData.BlockData[3 + index] + CurrentData.BlockData[5 + index];

                base6 = CurrentData.BlockData[1 + index] - CurrentData.BlockData[7 + index];
                base7 = temp1 + temp2;

                move0 = CurrentData.BlockData[0 + index];

                expended4 = ((base6 * 473 - base4 * 196 + 128) >> 8) - base7;
                expended0 = expended4 - (((temp1 - temp2) * 362 + 128) >> 8);
                expended1 = move0 - base1;
                expended2 = (((CurrentData.BlockData[2 + index] - CurrentData.BlockData[6 + index]) * 362 + 128) >> 8) - base3; // I forogt you had to add 6 + index not * I am so dumb uggh spent 20 minutes on this
                expended3 = move0 + base1;

                returned3 = expended1 + expended2;
                returned4 = expended3 + base3;
                returned5 = expended1 - expended2;
                returned6 = expended3 - base3;
                returned7 = -expended0 - ((base4 * 473 + base6 * 196 + 128) >> 8);

                //store
                CurrentData.BlockData[0 + index] = (base7 + returned4 + 128) >> 8; // shift 8 (block size)
                CurrentData.BlockData[1 + index] = (expended4 + returned3 + 128) >> 8;
                CurrentData.BlockData[2 + index] = (returned5 - expended0 + 128) >> 8;
                CurrentData.BlockData[3 + index] = (returned6 - returned7 + 128) >> 8;
                CurrentData.BlockData[4 + index] = (returned6 + returned7 + 128) >> 8;
                CurrentData.BlockData[5 + index] = (expended0 + returned5 + 128) >> 8;
                CurrentData.BlockData[6 + index] = (returned3 - expended4 + 128) >> 8;
                CurrentData.BlockData[7 + index] = (returned4 - base7 + 128) >> 8;
            }      
        }    
   
        public static void NextFrame() // this has to be called (or well should be) called in an loop, you don't have to bother with timings in your update loop the libary here shold handle it all, using a while(true) loop is fine, using a while loop with while (!Raylib.WindowShouldClose() works, this is just to avoid eating up an entire thread that your program maybe using (yeah idk why I included a while loop here in the libary orginal it was a stupid idea, thankfully i fixed, no more thread eating, i love to yap also)
        {
            int code = BitReader.FindNextMPEGStartCode();

            // here we are seeing what we need to do, either decode a dequence header or decode a picture and what not
            switch(code)
            {   
                case BudgetMPEGTables.START_SEQUENCE:
                    DecodeSequenceHeader();
                break;
                case BudgetMPEGTables.START_PICTURE:
                    if(CurrentData.IsPlaying)
                        NextFrameScheduler();
                    
                    DecodePicture();
                break;
                case BudgetMPEGTables.NOT_FOUND:
                    Stop();

                    if(CurrentData.Loop && CurrentData.SequenceStarted)
                        Play();
                break;
                default:
                    // ignore
                break;
            }

            debugFrameCounter++;
        }

        public static void NextFrameScheduler()
        {
            double stallTime = (1000 / CurrentData.Framerate) - CurrentData.LateTime; // how long we have to stall to match the framerate (we don't want to go out of sync with the framerate) 
            CurrentData.TargetTime = DateTime.Now.Millisecond + stallTime; // this is the target time for the next frame

            if(stallTime < 18) // if the stalltime is less than 18 (milliseconds iirc) we go to the next time
                NextFrame();
        } 

        public static void Play()
        {
            if(CurrentData.IsPlaying)
                return;

            CurrentData.IsPlaying = true;
            NextFrameScheduler();
        }

        public static void Pause()
        {
            CurrentData.IsPlaying = false;
        }

        public static void Stop()
        {
            BitReader.Index = CurrentData.FirstSequenceHeader;
            Pause();
        }

    }
}  