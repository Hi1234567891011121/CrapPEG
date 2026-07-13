using System;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text.Json;
using BudgetMPEGSharp.Libs;
using Raylib_cs;

namespace BudgetMPEGSharp
{
    public class V1Main
    {
        public static bool READ_USER = false;
        public static int SCREEN_AREA = 0;
        public V1Main()
        {
            BudgetMPEGSharpDecoder.CurrentData.VideoPath = "example_01.mpg";

            if(READ_USER)
            {
                Console.WriteLine($"Hello, Welcome To BudgetMPEGSharp!");
                Console.WriteLine($"Please, enter the file-name of the MPEG-1 video you want to view! Please note it much be in the same place where this program is being run in! So copy it to the same folder this program is being ran in!");
                
                BudgetMPEGSharpDecoder.CurrentData.VideoPath = Console.ReadLine();
            }

            BudgetMPEGSharpDecoder.Intaltize(); // you must Intaltize it here, this was just the eaiest way to make sure BitReader gets the right path
            Raylib.InitWindow(BudgetMPEGSharpDecoder.CurrentData.Width, BudgetMPEGSharpDecoder.CurrentData.Height, $"Now Playing: '{BudgetMPEGSharpDecoder.CurrentData.VideoPath}'");
 
            SCREEN_AREA = BudgetMPEGSharpDecoder.CurrentData.Width * BudgetMPEGSharpDecoder.CurrentData.Height;

            while (!Raylib.WindowShouldClose())
            {
                Draw();

                Update();
            }    
        }

        public void Update()
        {
            BudgetMPEGSharpDecoder.NextFrame(); // call the NextFrame in an Update loop (a while true would work a okay)! timings and everything are fine, the frameTimings are handles using date.now and such so you don't need a spefic target update rate for your loop
       
            // this is just for debugging (I need a way to just dump the current data at will)

            if(Raylib.IsKeyPressed(KeyboardKey.Space))
            {
                BudgetMPEGSharpDecoder.Pause();
            }
        }
  
        public void Draw()
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);

            // remeber row col (y,x) not (x,y) i forgot sometimes ):
            // colours are just stored as RGB, the FrameBuffer is the values of the current frame, the Decoder will continusly update this frame, so just draw from it
            // the framerate update is in the decoder, it should be fine, because we just listening and drawing the colours at the current time, it shouldn't have any sync issues?
           
           
            for (int i = 0; i < SCREEN_AREA; ++i) {
                int y = i / BudgetMPEGSharpDecoder.CurrentData.Width;
                int x = i % BudgetMPEGSharpDecoder.CurrentData.Width;

                Raylib.DrawPixel(x, y, new Color(BudgetMPEGSharpDecoder.CurrentData.FrameBuffer[i].Red, BudgetMPEGSharpDecoder.CurrentData.FrameBuffer[i].Green, BudgetMPEGSharpDecoder.CurrentData.FrameBuffer[i].Blue));
            }

            Raylib.EndDrawing();
        }
    }
}