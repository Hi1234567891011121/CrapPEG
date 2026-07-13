namespace CPEG
{
    public class BitReader
    {
        public byte[] Bytes { get; }
        public int Length { get; }
        public int Index { get; set; }
        public string Path { get; }

        public BitReader(string path)
        {
            Bytes = File.ReadAllBytes(path); // we are just dumping all of the bytes of the MPEG-1 file, and reading it (we read the bytes and do stuff)
            Length = Bytes.Length;
            Index = 0; // this where we are in the file 
            Path = path;
        }

        public const int NOT_FOUND = -1;

        public int FindNextMPEGStartCode()
        {
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

            return NOT_FOUND;
        }

        public bool NextBytesAreStartCode()
        { 
            int i = Index + 7 >> 3;
            return i >= Length || Bytes[i] == 0x00 && Bytes[i + 1] == 0x00 && Bytes[i + 2] == 0x01;
        }

        public int NextBits(int count)
        {
            int offset = Index >> 3; 
            int room = 8 - Index % 8; 
        
            if(room >= count)
                return (Bytes[offset] >> (room - count)) & (0xff >> (8 - count));
            
            int leftover = (Index + count) % 8; 
            int end = (Index + count -1) >> 3;
            int value = Bytes[offset] & (0xff >> (8 - room)); 

            for(offset++; offset < end; offset++)
            {
                value <<= 8; 
                value |= Bytes[offset];
            }

            if(leftover > 0)
            {
                value <<= leftover; 
                value |= Bytes[offset] >> (8 - leftover);    
            } else {
                value <<= 8;
                value |= Bytes[offset]; 
            }

            return value;
        }

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