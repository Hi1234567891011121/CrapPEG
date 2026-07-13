namespace BudgetMPEGSharp.Libs
{
    public static class BudgetMPEGUtils {
        public static BudgetMPEGColour GetColorFromYCbCr(int y, int cb, int cr, int a)
        {
            double Y = (double) y;
            double Cb = (double) cb;
            double Cr = (double) cr;

            int r = (int) (Y + 1.40200 * (Cr - 0x80));
            int g = (int) (Y - 0.34414 * (Cb - 0x80) - 0.71414 * (Cr - 0x80));
            int b = (int) (Y + 1.77200 * (Cb - 0x80));

            r = Math.Max(0, Math.Min(255, r));
            g = Math.Max(0, Math.Min(255, g));
            b = Math.Max(0, Math.Min(255, b));

            return new BudgetMPEGColour((byte) a, (byte) r, (byte) g, (byte) b);
        }
    }
}