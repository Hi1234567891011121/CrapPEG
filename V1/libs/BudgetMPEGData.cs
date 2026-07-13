using System.Text.Json.Serialization;

namespace BudgetMPEGSharp.Libs
{
    public struct BudgetMPEGData()
    {
        public string VideoPath { get; set; } = "default.mpeg"; // default path, it'll end up loading the path from the Decoder, but I just wanted it here, I need to make sure it all set before we do stuff I forgot.
        public string ByteSectionPath { get; set; } = ""; 
        public long FileSize {get; set;}  = 0;

        public double TargetTime { get; set; } = 0;
        public double LateTime { get; set; } = 0;
        public int FirstSequenceHeader { get; set; } = 0;

        public bool SequenceStarted { get; set; } = false;

        public int Height { get; set; } = 240; // standard VCD height (lines)
        public int Width { get; set; } = 352; // standard VCD width
        public string AspectRatio { get; set; } = ""; 
        public double Framerate { get; set; } = 0;
      
        public int BitRate { get; set; } = 0; 

        public byte[] IntraQuantMatrix { get; set; } = BudgetMPEGTables.DEFAULT_INTRA_QUANT_MATRIX; // intra is a 'full picture' (iirc)
        public byte[] CustomIntraQuantMatrix { get; set; } = BudgetMPEGTables.DEFAULT_INTRA_QUANT_MATRIX; // intra is a 'full picture' (iirc)
        public byte[] NonIntraQuantMatrix { get; set; } = BudgetMPEGTables.DEFAULT_NON_INTRA_QUANT_MATRIX; // inter is not it is inbetween these full frames
        public byte[] CustomNonIntraQuantMatrix { get; set; } = BudgetMPEGTables.DEFAULT_NON_INTRA_QUANT_MATRIX; // inter is not it is inbetween these full frames

        public int MacroBlockHeight { get; set; } = 0;
        public int MacroBlockWidth { get; set; } = 0;
        public int MacroBlockSize { get; set; } = 0;

        public int CodedHeight { get; set; } = 0;
        public int CodedWidth { get; set; } = 0;
        public int CodedSize { get; set; } = 0;

        public int HalfHeight { get; set; } = 0;
        public int HalfWidth { get; set; } = 0;
        public int QuarterSize { get; set; } = 0;

        // We JSON ignore these, because they clog up all the logs (they're SOOO many values)
        [JsonIgnore]
        public byte[]? CurrentY { get; set; } = null;
        [JsonIgnore]
        public byte[]? CurrentCr { get; set; } = null;
        [JsonIgnore]
        public byte[]? CurrentCb { get; set; } = null;

        [JsonIgnore] // JSON doesn't like serlizing this at all
        public BudgetMPEGColour[]? FrameBuffer { get; set; } = null;
        public int TempernalSequenceNumber { get; set; } = 0;
        public byte PictureCodingType { get; set; } = 0;
        public int VBVDelay { get; set; } = 0;

        [JsonIgnore]
        public byte[]? FowardY { get; set; } = null;
        [JsonIgnore]
        public byte[]? FowardCr { get; set; } = null;
        [JsonIgnore]
        public byte[]? FowardCb { get; set; } = null;

        public bool FullPixelFoward { get; set; } = false;
        public int FowardFCode { get; set; } = 0;
        public int ForwardRSize { get; set; } = 0;
        public int FowardF { get; set; } = 0;
        
        public int QuantizerScale { get; set; } = 0;
        public bool SliceBegin { get; set; } = false;

        public int MacroblockAddress { get; set; } = 0;

        public int MacroblockRow { get; set; } = 0;
        public int MacroblockCol { get; set; } = 0;
        
        public int MacroblockType { get; set; } = 0;
        public bool MacroblockIntra { get; set; } = false;        
        public bool MacroblockMotionFoward { get; set; } = false;
        
        public int MotionPreviousH { get; set; } = 0;
        public int MotionPreviousV { get; set; } = 0;

        public int MotionFowardH { get; set; } = 0;
        public int MotionFowardV { get; set; } = 0;

        public int DCPredictorY { get; set; } = 0;
        public int DCPredictorCr { get; set; } = 0;
        public int DCPredictorCb { get; set; } = 0;

        [JsonIgnore] // Too big
        public int[] BlockData { get; set; } = new int[64];

        public bool IsPlaying { get; set; } = true;
        public bool Loop { get; set; } = true;
    }
}