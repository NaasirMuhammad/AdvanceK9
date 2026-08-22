using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LemonUI;
using LemonUI.Menus;
using Rage;

namespace AdvancedK9
{
    internal sealed class K9Menu
    {
        private readonly ObjectPool _pool = new ObjectPool();
        private readonly NativeMenu _menu = new NativeMenu("ADVANCED K9", "SELECT AN OPTION");
        private readonly List<string> _items = new List<string>();
        private uint _nextInput;
        public bool Visible { get { return _menu.Visible; } private set { _menu.Visible = value; } }
        public string Title { get; private set; } = "ADVANCED K9";
        public event Action<int> Selected;
        public event Action<int, int> Adjusted;

        public K9Menu()
        {
            _pool.Add(_menu);
            _menu.Width = 0.235f;
            _menu.Offset = new PointF(16f, 22f);
            _menu.MouseBehavior = MenuMouseBehavior.Disabled;
        }

        public void Open(string title, IEnumerable<string> items) { Rebuild(title, items, 0); }
        public void Update(string title, IEnumerable<string> items) { Rebuild(title, items, Math.Max(0, _menu.SelectedIndex)); }
        public void Close() { Visible = false; }

        public void Tick()
        {
            _pool.Process();
            if (!Visible || Game.GameTime < _nextInput) return;
            if (Game.IsKeyDown(Keys.Left)) { Adjusted?.Invoke(_menu.SelectedIndex, -1); Delay(140); }
            else if (Game.IsKeyDown(Keys.Right)) { Adjusted?.Invoke(_menu.SelectedIndex, 1); Delay(140); }
        }

        private void Delay(uint milliseconds) { _nextInput = Game.GameTime + milliseconds; }

        private void Rebuild(string title, IEnumerable<string> items, int selected)
        {
            Title = title;
            _items.Clear();
            _items.AddRange(items ?? Enumerable.Empty<string>());
            _menu.Clear();
            _menu.BannerText.Text = title;
            for (int index = 0; index < _items.Count; index++)
            {
                int captured = index;
                var item = new NativeItem(_items[index], "Enter activates this option. Left and right preview adjustable options.");
                item.Activated += (sender, args) => Selected?.Invoke(captured);
                _menu.Add(item);
            }
            if (_items.Count > 0) _menu.SelectedIndex = Math.Min(selected, _items.Count - 1);
            Visible = true;
        }
    }
}
