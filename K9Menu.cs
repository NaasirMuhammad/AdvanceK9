using System; using System.Collections.Generic; using System.Windows.Forms; using Rage; using Rage.Native;
namespace AdvancedK9
{
    internal sealed class K9Menu
    {
        readonly List<string> _items=new List<string>(); int _selected; uint _nextInput; public bool Visible{get;private set;} public string Title{get;private set;}="ADVANCED K9"; public event Action<int> Selected;
        public void Open(string title,IEnumerable<string> items){Title=title;_items.Clear();_items.AddRange(items);_selected=0;Visible=true;} public void Close(){Visible=false;}
        public void Tick(){if(!Visible)return;Draw();if(Game.GameTime<_nextInput)return;if(Game.IsKeyDown(Keys.Escape)||Game.IsKeyDown(Keys.Back)){Close();_nextInput=Game.GameTime+180;return;}if(Game.IsKeyDown(Keys.Up)){_selected=(_selected-1+_items.Count)%_items.Count;_nextInput=Game.GameTime+150;}else if(Game.IsKeyDown(Keys.Down)){_selected=(_selected+1)%_items.Count;_nextInput=Game.GameTime+150;}else if(Game.IsKeyDown(Keys.Enter)){Selected?.Invoke(_selected);_nextInput=Game.GameTime+180;}}
        void Draw(){float x=.205f,y=.18f,w=.30f,row=.035f;int visible=Math.Min(15,_items.Count),start=Math.Max(0,Math.Min(_selected-visible/2,_items.Count-visible));Rect(x,y,w,.055f,12,38,65,235);Text(Title,x-w/2+.012f,y-.017f,.42f,255,255,255,255);for(int line=0;line<visible;line++){int i=start+line;float iy=y+.048f+line*row;Rect(x,iy,w,row,i==_selected?35:8,i==_selected?115:18,i==_selected?180:28,225);Text((i==_selected?"> ":"  ")+_items[i],x-w/2+.01f,iy-.012f,.30f,255,255,255,255);}float fy=y+.048f+visible*row;Rect(x,fy,w,.030f,4,12,20,220);Text("Arrows: Navigate   Enter: Select   Esc: Close",x-w/2+.01f,fy-.010f,.245f,190,215,235,255);}
        static void Rect(float x,float y,float w,float h,int r,int g,int b,int a)=>NativeFunction.Natives.DRAW_RECT(x,y,w,h,r,g,b,a);
        static void Text(string v,float x,float y,float s,int r,int g,int b,int a){NativeFunction.Natives.SET_TEXT_FONT(0);NativeFunction.Natives.SET_TEXT_SCALE(s,s);NativeFunction.Natives.SET_TEXT_COLOUR(r,g,b,a);NativeFunction.Natives.SET_TEXT_OUTLINE();NativeFunction.Natives.BEGIN_TEXT_COMMAND_DISPLAY_TEXT("STRING");NativeFunction.Natives.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME(v);NativeFunction.Natives.END_TEXT_COMMAND_DISPLAY_TEXT(x,y);}
    }
}
