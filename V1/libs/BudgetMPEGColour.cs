/*
    I didn't want to use the built in Drawing from System really, there's a whole lot of data 
    that's not needed. I really just need the Red Green and Blue, since this is the data you're reading
    from the libary to display. 
*/
namespace BudgetMPEGSharp.Libs
{
    public class BudgetMPEGColour
    {
        public BudgetMPEGColour(byte red, byte green, byte blue, byte alpha = 255)
        {
            Red = red;
            Green = green;
            Blue = green;
            Alpha = alpha;
        }

        public byte Red { get; set; } = 255;
        public byte Green { get; set; } = 255;
        public byte Blue { get; set; }  = 255;
        public byte Alpha { get; set; }  = 255;
    
        public override string ToString()
        {
            return $"({Red},{Green},{Blue},{Alpha})";
        }
    }
}