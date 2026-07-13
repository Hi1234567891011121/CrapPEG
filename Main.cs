using Raylib_cs;

namespace CPEG {
    public class Main {

        public static BitReader reader = new("example_02.mpg");
        public CrapPEG crap = new CrapPEG(reader);

        public int screenArea;

        public Main() {

            Raylib.InitWindow(crap.width, crap.height, "Crap");
            Raylib.SetConfigFlags(ConfigFlags.VSyncHint);
            while (!Raylib.WindowShouldClose())
            {
                crap.NextFrame();
                Draw();
            }   
        }

        public void Draw()
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);

            // remeber row col (y,x) not (x,y) i forgot sometimes ):
            // colours are just stored as RGB, the FrameBuffer is the values of the current frame, the Decoder will continusly update this frame, so just draw from it
            // the framerate update is in the decoder, it should be fine, because we just listening and drawing the colours at the current time, it shouldn't have any sync issues?
              
            for (int i = 0; i < crap.currentRGBA.Length; ++i) {
                int y = i / crap.width;
                int x = i % crap.width;

                Raylib.DrawPixel(x, y, new Color(crap.currentRGBA[i].R, crap.currentRGBA[i].G, crap.currentRGBA[i].B));
            }

            Raylib.EndDrawing();
        }
    }
}
