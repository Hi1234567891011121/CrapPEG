/*
    Praise be this https://link.springer.com/chapter/10.1007/0-306-46983-9_8
    Praise be this https://www.globalspec.com/reference/37862/203279/chapter-7-mpeg-system-syntax
    Praise be this http://dvdnav.mplayerhq.hu/dvdinfo/mpeghdrs.html (this is MPEG-2 though)

    Once this works well enough, I'll deal with using the lowest value bits I can to save memory.

    Oh FYI |= is a bitwise for OR.
*/

namespace BudgetMPEGSharp.Libs
{
    public class BudgetMPEGBitReader
    {
        public byte[] Bytes { get; }
        public int Length { get; }
        public int Index { get; set; }

        public BudgetMPEGBitReader()
        {
            Bytes = File.ReadAllBytes(BudgetMPEGSharpDecoder.CurrentData.VideoPath); // we are just dumping all of the bytes of the MPEG-1 file, and reading it (we read the bytes and do stuff)
            Length = Bytes.Length;
            Index = 0; // this where we are in the file 
        }

        public int FindNextMPEGStartCode()
        {
            // we are looking for the start to each section of MPEG, first 24 bytes are width and height, and so on
            for(int i = Index + 7 >> 3; i < Length; i++) {
                if( Bytes[i] == 0x00
                    && Bytes[i + 1] == 0x00
                    && Bytes[i + 2] == 0x01
                ) {
                    Index = (i + 4) << 3;
                    return Bytes[i + 3];
                }
            }
            
            Index <<= 3;
            return BudgetMPEGTables.NOT_FOUND;
        }

        public bool NextBytesAreStartCode()
        { 
            int i = Index + 7 >> 3; // same idea as the other method we are just returning a bool
            return i >= Length || Bytes[i] == 0x00 && Bytes[i + 1] == 0x00 && Bytes[i + 2] == 0x01;
        }

        public int NextBits(int count)
        {
            /*
                This is a lot easier than I first thought (no one seems to comment their code lol, 
                so on your own to understand).

                All we are doing is reading the bytes starting at the index, and ending 
                when reading the bits from that to the end of count. Let's say the index is
                at bit one, and the count is 8, we count from bit one to bit 9. We have to 
                keep track of a few things, for the method leftover bits, and whatever, make 
                sure we have an understanding of bytes and all, remainders. But to use it is very
                easy. Much easier than I thought.

                on it later though. Because the decoder isn't fully done, and perhaps it'll sort
                itself out or something.
                March 10: i may need to enhance checking, it gets out of bounds, will work
            */

            int offset = Index >> 3; // yep you know the drill already
            int room = 8 - Index % 8; // 8 bits = 1 byte of course
        
            if(room >= count)
                return (Bytes[offset] >> (room - count)) & (0xff >> (8 - count));
            
            int leftover = (Index + count) % 8; // left over bits
            int end = (Index + count -1) >> 3;
            int value = Bytes[offset] & (0xff >> (8 - room)); // fill the 'ol first byte

            for(offset++; offset < end; offset++)
            {
                value <<= 8; 
                value |= Bytes[offset];
            }

            if(leftover > 0)
            {
                value <<= leftover; // room
                value |= Bytes[offset] >> (8 - leftover);    
            } else {
                value <<= 8;
                value |= Bytes[offset]; // if your IDE tells you you can disgard it or do _ DON'T you must OR like this!!!!
            }

            return value;
        }

        /// <summary>
        /// Method <c>GetBits</c> gets the value of the bits between the current index and the end of count.
        /// </summary>
        public int GetBits(int count)
        {
            int value = NextBits(count);
            Index += count;

            return value;
        }

        public int AdvanceBits(int count)
        {
            return Index += count;
        }

        public int RewindBits(int count)
        {
            return Index -= count;
        }
    }
}