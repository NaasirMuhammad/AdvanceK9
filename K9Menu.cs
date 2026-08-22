using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Rage;
using Rage.Native;

namespace AdvancedK9
{
    internal sealed class K9Menu
    {
        private readonly List<string> _items = new List<string>();
        private int _selected;
        private uint _nextInput;
        public bool Visible { get; private set; }
        public string Title { get; private set; } = "ADVANCED K9";
        public event Action<int> Selected;
        public event Action<int, int> Adjusted;

        public void Open(string title, IEnumerable<string> items)
        {
            Title = title; _items.Clear(); _items.AddRange(items); _selected = 0; Visible = true;
        }

        public void Update(string title, IEnumerable<string> items)
        {
            Title = title; _items.Clear(); _items.AddRange(items);
            _selected = Math.Max(0, Math.Min(_selected, _items.Count - 1)); Visible = true;
        }

        public void Close() { Visible = false; }

        public void Tick()
        {
            if (!Visible) return;
            Draw();
            if (Game.GameTime < _nextInput) return;
            if (Game.IsKeyDown(Keys.Escape) || Game.IsKeyDown(Keys.Back)) { Close(); Delay(180); return; }
            if (Game.IsKeyDown(Keys.Up)) { _selected = Wrap(_selected - 1, _items.Count); Delay(125); }
            else if (Game.IsKeyDown(Keys.Down)) { _selected = Wrap(_selected + 1, _items.Count); Delay(125); }
            else if (Game.IsKeyDown(Keys.Left)) { Adjusted?.Invoke(_selected, -1); Delay(115); }
            else if (Game.IsKeyDown(Keys.Right)) { Adjusted?.Invoke(_selected, 1); Delay(115); }
            else if (Game.IsKeyDown(Keys.Enter)) { Selected?.Invoke(_selected); Delay(160); }
        }

        private void Delay(uint milliseconds) { _nextInput = Game.GameTime + milliseconds; }
        private static int Wrap(int value, int count) { return count <= 0 ? 0 : (value % count + count) % count; }

        private void Draw()
        {
            float x = .17f, y = .16f, width = .265f, row = .029f;
            int visible = Math.Min(10, _items.Count);
            int start = Math.Max(0, Math.Min(_selected - visible / 2, _items.Count - visible));
            Rect(x, y, width, .046f, 8, 29, 47, 242);
            Rect(x, y - .021f, width, .004f, 35, 145, 215, 255);
            Text(Title, x - width / 2 + .010f, y - .014f, .34f, 245, 250, 255, 255);
            for (int line = 0; line < visible; line++)
            {
                int index = start + line; float itemY = y + .039f + line * row; bool active = index == _selected;
                Rect(x, itemY, width, row - .001f, active ? 24 : 5, active ? 105 : 16, active ? 166 : 25, 232);
                Text((active ? "› " : "  ") + _items[index], x - width / 2 + .009f, itemY - .010f, .255f, 244, 249, 255, 255);
            }
            float footerY = y + .039f + visible * row;
            Rect(x, footerY, width, .027f, 3, 11, 18, 232);
            Text("↑↓ Select   ←→ Preview   Enter Action   Esc Close", x - width / 2 + .008f, footerY - .009f, .205f, 175, 208, 230, 255);
        }

        private static void Rect(float x, float y, float w, float h, int r, int g, int b, int a) => NativeFunction.Natives.DRAW_RECT(x, y, w, h, r, g, b, a);
        private static void Text(string value, float x, float y, float scale, int r, int g, int b, int a)
        {
            NativeFunction.Natives.SET_TEXT_FONT(0); NativeFunction.Natives.SET_TEXT_SCALE(scale, scale);
            NativeFunction.Natives.SET_TEXT_COLOUR(r, g, b, a); NativeFunction.Natives.SET_TEXT_OUTLINE();
            NativeFunction.Natives.BEGIN_TEXT_COMMAND_DISPLAY_TEXT("STRING");
            NativeFunction.Natives.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME(value);
            NativeFunction.Natives.END_TEXT_COMMAND_DISPLAY_TEXT(x, y);
        }
    }
}
