using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ImGuiNET
{
    public static class ImGuiHelper
    {
        private struct KeyRepeat
        {
            public char ch;
            public Keys key;
            public float delay;
        }

        private static float _scaleFactor = 1;
        private static float _wheelPosition;
        private static List<KeyRepeat> _repeatDelay = new List<KeyRepeat>();

        private static ImGuiVertex[] vertices = new ImGuiVertex[1024];
        private static short[] indices = new short[256];
        private static Texture2D fontTexture;
        private static KeyboardState kbState;
        private static VertexBuffer _vertexBuffer;
        private static IndexBuffer _indexBuffer;
        private static Game _game;

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        private struct ImGuiVertex : IVertexType
        {
            public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
                 new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.Position, 0),
                 new VertexElement(8, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
                 new VertexElement(16, VertexElementFormat.Color, VertexElementUsage.Color, 0)
                );

            public float x;
            public float y;
            //public float fill;
            public float tx;
            public float ty;
            public uint color;

            VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
        }

        #region Convert Key
        public static char ConvertKeyboardInput(Keys key, bool alt, bool shift, bool ctrl, bool caps)
        {
            var upperCase = caps ^ shift;
            switch (key)
            {
                //Alphabet keys
                case Keys.A: return upperCase ? 'A' : 'a';
                case Keys.B: return upperCase ? 'B' : 'b';
                case Keys.C: return upperCase ? 'C' : 'c';
                case Keys.D: return upperCase ? 'D' : 'd';
                case Keys.E: return upperCase ? 'E' : 'e';
                case Keys.F: return upperCase ? 'F' : 'f';
                case Keys.G: return upperCase ? 'G' : 'g';
                case Keys.H: return upperCase ? 'H' : 'h';
                case Keys.I: return upperCase ? 'I' : 'i';
                case Keys.J: return upperCase ? 'J' : 'j';
                case Keys.K: return upperCase ? 'K' : 'k';
                case Keys.L: return upperCase ? 'L' : 'l';
                case Keys.M: return upperCase ? 'M' : 'm';
                case Keys.N: return upperCase ? 'N' : 'n';
                case Keys.O: return upperCase ? 'O' : 'o';
                case Keys.P: return upperCase ? 'P' : 'p';
                case Keys.Q: return upperCase ? 'Q' : 'q';
                case Keys.R: return upperCase ? 'R' : 'r';
                case Keys.S: return upperCase ? 'S' : 's';
                case Keys.T: return upperCase ? 'T' : 't';
                case Keys.U: return upperCase ? 'U' : 'u';
                case Keys.V: return upperCase ? 'V' : 'v';
                case Keys.W: return upperCase ? 'W' : 'w';
                case Keys.X: return upperCase ? 'X' : 'x';
                case Keys.Y: return upperCase ? 'Y' : 'y';
                case Keys.Z: return upperCase ? 'Z' : 'z';
                //Decimal keys
                case Keys.D0: return shift ? ')' : '0';
                case Keys.D1: return shift ? '!' : '1';
                case Keys.D2: return shift ? '@' : '2';
                case Keys.D3: return shift ? '#' : '3';
                case Keys.D4: return shift ? '$' : '4';
                case Keys.D5: return shift ? '%' : '5';
                case Keys.D6: return shift ? '^' : '6';
                case Keys.D7: return shift ? '&' : '7';
                case Keys.D8: return shift ? '*' : '8';
                case Keys.D9: return shift ? '(' : '9';

                //Decimal numpad keys
                case Keys.NumPad0: return '0';
                case Keys.NumPad1: return '1';
                case Keys.NumPad2: return '2';
                case Keys.NumPad3: return '3';
                case Keys.NumPad4: return '4';
                case Keys.NumPad5: return '5';
                case Keys.NumPad6: return '6';
                case Keys.NumPad7: return '7';
                case Keys.NumPad8: return '8';
                case Keys.NumPad9: return '9';

                //Special keys
                case Keys.OemTilde: return shift ? '~' : '`';
                case Keys.OemSemicolon: return shift ? ':' : ';';
                case Keys.OemQuotes: return shift ? '"' : '\'';
                case Keys.OemQuestion: return shift ? '?' : '/';
                case Keys.OemPlus: return shift ? '+' : '=';
                case Keys.OemPipe: return shift ? '|' : '\\';
                case Keys.OemPeriod: return shift ? '>' : '.';
                case Keys.OemOpenBrackets: return shift ? '{' : '[';
                case Keys.OemCloseBrackets: return shift ? '}' : ']';
                case Keys.OemMinus: return shift ? '_' : '-';
                case Keys.OemComma: return shift ? '<' : ',';
                case Keys.Space: return ' ';
            }

            return '\0';
        }
        #endregion

        public static unsafe void Initiailize(Game game)
        {
            _game = game;
            var graphicsDevice = _game.GraphicsDevice;
            _vertexBuffer = new VertexBuffer(graphicsDevice, ImGuiVertex.VertexDeclaration, 1024, BufferUsage.WriteOnly);
            _indexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, 256, BufferUsage.WriteOnly);

            var io = ImGui.GetIO();
            io.KeyMap[GuiKey.Tab] = (int)Keys.Tab;
            io.KeyMap[GuiKey.LeftArrow] = (int)Keys.Left;
            io.KeyMap[GuiKey.RightArrow] = (int)Keys.Right;
            io.KeyMap[GuiKey.UpArrow] = (int)Keys.Up;
            io.KeyMap[GuiKey.DownArrow] = (int)Keys.Down;
            io.KeyMap[GuiKey.PageUp] = (int)Keys.PageUp;
            io.KeyMap[GuiKey.PageDown] = (int)Keys.PageDown;
            io.KeyMap[GuiKey.Home] = (int)Keys.Home;
            io.KeyMap[GuiKey.End] = (int)Keys.End;
            io.KeyMap[GuiKey.Delete] = (int)Keys.Delete;
            io.KeyMap[GuiKey.Backspace] = (int)Keys.Back;
            io.KeyMap[GuiKey.Enter] = (int)Keys.Enter;
            io.KeyMap[GuiKey.Escape] = (int)Keys.Escape;
            io.KeyMap[GuiKey.A] = (int)Keys.A;
            io.KeyMap[GuiKey.C] = (int)Keys.C;
            io.KeyMap[GuiKey.V] = (int)Keys.V;
            io.KeyMap[GuiKey.X] = (int)Keys.X;
            io.KeyMap[GuiKey.Y] = (int)Keys.Y;
            io.KeyMap[GuiKey.Z] = (int)Keys.Z;

            //io.FontAtlas.AddFontFromFileTTF("C:\\windows\\fonts\\segoeui.ttf", 24);
            io.FontAtlas.AddDefaultFont();

            // Build texture atlas
            FontTextureData texData = io.FontAtlas.GetTexDataAsAlpha8();
            var colorData = new Color[texData.Width * texData.Height];
            var pixels = texData.Pixels;
            for (var i = 0; i < colorData.Length; i++)
            {
                var v = pixels[i];
                colorData[i] = new Color(1f, 1f, 1f, 255f / v) * (255f / v);
            }

            fontTexture = new Texture2D(graphicsDevice, texData.Width, texData.Height);
            fontTexture.SetData(colorData);

            io.FontAtlas.SetTexID(0);
            io.FontAtlas.ClearTexData();

        }

        public static void newFrame(GameTime gt)
        {
            IO io = ImGui.GetIO();
            var graphicsDevice = _game.GraphicsDevice;
            var width = graphicsDevice.PresentationParameters.BackBufferWidth;
            var height = graphicsDevice.PresentationParameters.BackBufferHeight;
            io.DisplaySize = new Vector2(width, height);
            io.DisplayFramebufferScale = new Vector2(_scaleFactor);
            io.DeltaTime = (float)gt.ElapsedGameTime.TotalSeconds;


            //MouseState cursorState = MouseCursor.ge  Mouse.get();
            MouseState mouseState = Mouse.GetState();

            if (_game.IsActive)
            {
                //Point windowPoint = _nativeWindow.PointToClient(new Point(cursorState.X, cursorState.Y));
                Point windowPoint = new Point(mouseState.X, mouseState.Y);
                io.MousePosition = new Vector2(windowPoint.X / io.DisplayFramebufferScale.X, windowPoint.Y / io.DisplayFramebufferScale.Y);
            }
            else
            {
                io.MousePosition = new Vector2(-1f, -1f);
            }

            io.MouseDown[0] = mouseState.LeftButton == ButtonState.Pressed;
            io.MouseDown[1] = mouseState.RightButton == ButtonState.Pressed;
            io.MouseDown[2] = mouseState.MiddleButton == ButtonState.Pressed;

            float newWheelPos = mouseState.ScrollWheelValue;
            float delta = newWheelPos - _wheelPosition;
            _wheelPosition = newWheelPos;
            io.MouseWheel = delta;

            var newKbState = Keyboard.GetState();
            var keys = kbState.GetPressedKeys();
            for (var i = 0; i < keys.Length; i++)
                io.KeysDown[(int)keys[i]] = false;

            keys = newKbState.GetPressedKeys();
            for (var i = 0; i < keys.Length; i++)
                io.KeysDown[(int)keys[i]] = true;

            for (int i = _repeatDelay.Count - 1; i >= 0; i--)
            {
                if (newKbState.IsKeyUp(_repeatDelay[i].key))
                {
                    var lastIndex = _repeatDelay.Count - 1;
                    _repeatDelay[i] = _repeatDelay[lastIndex];
                    _repeatDelay.RemoveAt(lastIndex);
                }
                else
                {
                    var r = _repeatDelay[i];
                    r.delay -= io.DeltaTime;
                    _repeatDelay[i] = r;
                }
            }

            io.AltPressed = newKbState.IsKeyDown(Keys.LeftAlt) || newKbState.IsKeyDown(Keys.RightAlt);
            io.ShiftPressed = (newKbState.IsKeyDown(Keys.LeftShift) || newKbState.IsKeyDown(Keys.RightShift));
            io.CtrlPressed = newKbState.IsKeyDown(Keys.LeftControl) || newKbState.IsKeyDown(Keys.RightControl);

            kbState = newKbState;

            for (var i = 0; i < keys.Length; i++)
            {
                var ch = ConvertKeyboardInput(keys[i], io.AltPressed, io.ShiftPressed, io.CtrlPressed, newKbState.CapsLock);
                if (ch != '\0')
                {
                    var index = -1;
                    for (var k = 0; k < _repeatDelay.Count; k++)
                    {
                        if (_repeatDelay[k].ch == ch)
                        {
                            index = k;
                            break;
                        }
                    }

                    if (index == -1)
                    {
                        _repeatDelay.Add(new KeyRepeat() { ch = ch, key = keys[i], delay = 0.25f });
                        ImGui.AddInputCharacter(ch);
                    }
                    else if (_repeatDelay[index].delay <= 0)
                    {
                        var r = _repeatDelay[index];
                        r.delay += 0.05f;
                        _repeatDelay[index] = r;
                        ImGui.AddInputCharacter(ch);
                    }
                }
            }

            ImGui.NewFrame();
        }

        public static unsafe void render(BasicEffect effect)
        {
            IO io = ImGui.GetIO();

            ImGui.Render();

            DrawData* draw_data = ImGui.GetDrawData();
            var graphicsDevice = _game.GraphicsDevice;
            graphicsDevice.DepthStencilState = DepthStencilState.None;
            graphicsDevice.RasterizerState = RasterizerState.CullNone;
            graphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
            graphicsDevice.BlendState = BlendState.AlphaBlend;

            // Handle cases of screen coordinates != from framebuffer coordinates (e.g. retina displays)
            ImGui.ScaleClipRects(draw_data, io.DisplayFramebufferScale);

            //var effect = Assets.defaultEffect;
            //effect.Texture = Assets.dummyTexture;
            effect.World = Matrix.Identity;
            effect.View = Matrix.Identity;// camera.viewMatrix3D;
            effect.Projection =
                Matrix.CreateOrthographicOffCenter(
                    0.0f,
                    io.DisplaySize.X / io.DisplayFramebufferScale.X,
                    io.DisplaySize.Y / io.DisplayFramebufferScale.Y,
                    0.0f,
                    -1.0f,
                    1.0f
                );

            // Render command lists

            for (int n = 0; n < draw_data->CmdListsCount; n++)
            {
                NativeDrawList* cmd_list = draw_data->CmdLists[n];
                DrawVert* vtx_buffer = (DrawVert*)cmd_list->VtxBuffer.Data;
                ushort* idx_buffer = (ushort*)cmd_list->IdxBuffer.Data;
                var vertexElements = cmd_list->VtxBuffer.Size;
                var indexElements = cmd_list->IdxBuffer.Size;
                var idxPos = 0;

                if (vertices.Length < vertexElements)
                {
                    vertices = new ImGuiVertex[vertexElements / 2 * 3];
                    _vertexBuffer.Dispose();
                    _vertexBuffer = new VertexBuffer(graphicsDevice, ImGuiVertex.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
                }

                for (int i = 0; i < vertexElements; i++)
                {
                    DrawVert vert = *vtx_buffer++;
                    vertices[i].x = vert.pos.X;
                    vertices[i].y = vert.pos.Y;
                    vertices[i].tx = vert.uv.X;
                    vertices[i].ty = vert.uv.Y;
                    vertices[i].color = vert.col;
                }

                _vertexBuffer.SetData(vertices);

                if (indices.Length < indexElements)
                {
                    indices = new short[indexElements / 2 * 3];
                    _indexBuffer.Dispose();
                    _indexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
                }

                for (int i = 0; i < indices.Length; i++) { indices[i] = (short)idx_buffer[i]; }

                graphicsDevice.Indices = _indexBuffer;
                graphicsDevice.SetVertexBuffer(_vertexBuffer);

                for (int cmd_i = 0; cmd_i < cmd_list->CmdBuffer.Size; cmd_i++)
                {
                    DrawCmd* pcmd = &(((DrawCmd*)cmd_list->CmdBuffer.Data)[cmd_i]);
                    if (pcmd->UserCallback != IntPtr.Zero)
                    {
                        throw new NotImplementedException();
                    }
                    else
                    {
                        var primivites = (int)pcmd->ElemCount / 3;
                        graphicsDevice.ScissorRectangle =
                            new Rectangle(
                                (int)pcmd->ClipRect.X,
                                (int)(io.DisplaySize.Y - pcmd->ClipRect.W),
                                (int)(pcmd->ClipRect.Z - pcmd->ClipRect.X),
                                (int)(pcmd->ClipRect.W - pcmd->ClipRect.Y)
                            );

                        effect.Texture = fontTexture;

                        foreach (var pass in effect.CurrentTechnique.Passes)
                        {
                            pass.Apply();
                            graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, idxPos, (int)pcmd->ElemCount / 3);
                            graphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, vertexElements, indices, 0, indexElements / 3);
                        }
                    }
                    idxPos += (int)pcmd->ElemCount * 3;
                }
            }
        }


    }
}
