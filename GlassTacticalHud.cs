using System;
using System.Drawing;
using System.IO;
using Rage;

namespace AdvancedK9
{
    internal sealed class GlassTacticalHud : IDisposable
    {
        internal sealed class Snapshot
        {
            public bool Visible,Collapsed,ShowPortrait,ShowState,ShowHealth,ShowStamina,ShowDistance,ShowCommand,ShowBehavior,ShowSearchProgress;
            public float X,Y,Scale,Opacity,Distance;
            public int Health,Stamina,SearchProgress;
            public string Name,State,Command,Behavior,SearchLabel,Alert,PortraitFile,Breed,Model,AppearanceKey;
            public bool Metric;
        }

        private readonly object _sync=new object();
        private Snapshot _snapshot=new Snapshot();
        private Texture _portrait;
        private string _portraitKey="";
        private bool _disposed;

        public GlassTacticalHud(){Game.RawFrameRender+=Render;}

        public void Update(Snapshot value)
        {
            if(value==null)return;
            lock(_sync)_snapshot=value;
            string key=(value.PortraitFile??"")+"|"+(value.Model??"")+"|"+(value.Breed??"")+"|"+(value.AppearanceKey??"");
            if(value.ShowPortrait&&key!=_portraitKey){_portraitKey=key;LoadPortrait(value);}
        }

        private void LoadPortrait(Snapshot value)
        {
            try
            {
                DisposePortrait();
                string root=Path.Combine("Plugins","LSPDFR","AdvancedK9","Portraits");
                string configured=ResolvePortraitPath(value.PortraitFile);
                string model=Path.Combine(root,SafeName(value.Model)+".png");
                string breed=Path.Combine(root,SafeName(value.Breed)+".png");
                string fallback=Path.Combine(root,"default.png");
                string selected=File.Exists(configured)?configured:File.Exists(model)?model:File.Exists(breed)?breed:File.Exists(fallback)?fallback:null;
                if(selected!=null){_portrait=Game.CreateTextureFromFile(selected);Game.LogTrivial("AdvancedK9 HUD portrait cached: "+selected);}
                else Game.LogTrivial("AdvancedK9 HUD portrait fallback: no custom/model/breed image; using breed badge.");
            }
            catch(Exception ex){DisposePortrait();Game.LogTrivial("AdvancedK9 HUD portrait ignored safely: "+ex.Message);}
        }

        private static string ResolvePortraitPath(string value)
        {
            if(string.IsNullOrWhiteSpace(value))return "";
            return Path.IsPathRooted(value)?value:Path.GetFullPath(value);
        }
        private static string SafeName(string value){if(string.IsNullOrWhiteSpace(value))return "unknown";foreach(char c in Path.GetInvalidFileNameChars())value=value.Replace(c,'_');return value.Replace(' ','_').ToLowerInvariant();}

        private void Render(object sender,GraphicsEventArgs args)
        {
            Snapshot s;lock(_sync)s=_snapshot;
            if(_disposed||s==null||!s.Visible)return;
            try
            {
                Size resolution=Game.Resolution;
                float scale=Math.Max(.55f,Math.Min(1.35f,s.Scale));
                if(s.Collapsed)scale*=.70f;
                float width=(s.Collapsed?205f:330f)*scale;
                float height=(s.Collapsed?38f:126f)*scale;
                float cx=Math.Max(width/2+12,Math.Min(resolution.Width-width/2-12,s.X*resolution.Width));
                float cy=Math.Max(height/2+12,Math.Min(resolution.Height-height/2-12,s.Y*resolution.Height));
                var box=new RectangleF(cx-width,cy-height,width,height);
                int alpha=(int)(Math.Max(.35f,Math.Min(1f,s.Opacity))*218);
                DrawGlass(args.Graphics,box,alpha);
                if(s.Collapsed){DrawText(args.Graphics,"K9 "+Upper(s.Name)+"  •  "+Upper(s.State),box.X+13*scale,box.Y+10*scale,17*scale,Color.White);return;}

                float portrait=72f*scale;
                float left=box.X+12f*scale;
                if(s.ShowPortrait)
                {
                    var portraitBox=new RectangleF(left,box.Y+13f*scale,portrait,portrait);
                    args.Graphics.DrawRectangle(portraitBox,Color.FromArgb(235,35,215,235));
                    if(_portrait!=null)args.Graphics.DrawTexture(_portrait,new RectangleF(portraitBox.X+3,portraitBox.Y+3,portraitBox.Width-6,portraitBox.Height-6));
                    else
                    {
                        args.Graphics.DrawRectangle(new RectangleF(portraitBox.X+3,portraitBox.Y+3,portraitBox.Width-6,portraitBox.Height-6),Color.FromArgb(190,20,31,39));
                        DrawText(args.Graphics,BreedBadge(s.Breed),portraitBox.X+13*scale,portraitBox.Y+21*scale,21*scale,Color.FromArgb(255,45,225,240));
                    }
                    left+=portrait+12f*scale;
                }
                DrawText(args.Graphics,"K9 "+Upper(s.Name),left,box.Y+10f*scale,20f*scale,Color.White);
                if(s.ShowState)DrawText(args.Graphics,Upper(s.State),box.Right-91f*scale,box.Y+12f*scale,14f*scale,StateColor(s.State));
                float row=box.Y+42f*scale;
                if(s.ShowHealth){DrawMeter(args.Graphics,"HEALTH",s.Health,left,row,box.Right-left-12f*scale,Color.FromArgb(255,58,210,118),scale);row+=25f*scale;}
                if(s.ShowStamina){DrawMeter(args.Graphics,"STAMINA",s.Stamina,left,row,box.Right-left-12f*scale,Color.FromArgb(255,35,214,235),scale);row+=25f*scale;}
                if(!s.ShowHealth&&!s.ShowStamina)row+=12f*scale;
                string bottom="";
                if(s.ShowCommand&&!string.IsNullOrWhiteSpace(s.Command))bottom=Upper(s.Command);
                if(s.ShowDistance){float value=s.Metric?s.Distance:s.Distance*3.28084f;bottom=Join(bottom,value.ToString("0.0")+(s.Metric?" m":" ft"));}
                if(s.ShowBehavior)bottom=Join(bottom,Upper(s.Behavior));
                DrawText(args.Graphics,bottom,left,box.Bottom-24f*scale,14f*scale,Color.FromArgb(255,208,231,236));
                if(!string.IsNullOrWhiteSpace(s.SearchLabel))
                {
                    string search=Upper(s.SearchLabel)+(s.ShowSearchProgress?"  "+Math.Max(0,Math.Min(100,s.SearchProgress))+"%":"");
                    DrawText(args.Graphics,search,left,box.Bottom-45f*scale,14f*scale,Color.FromArgb(255,45,225,240));
                }
                if(!string.IsNullOrWhiteSpace(s.Alert))
                {
                    var alert=new RectangleF(box.X,box.Y-32f*scale,box.Width,27f*scale);
                    args.Graphics.DrawRectangle(alert,Color.FromArgb(235,55,40,10));
                    args.Graphics.DrawRectangle(alert,Color.FromArgb(255,244,174,45));
                    args.Graphics.DrawRectangle(new RectangleF(alert.X+2,alert.Y+2,alert.Width-4,alert.Height-4),Color.FromArgb(235,55,40,10));
                    DrawText(args.Graphics,"K9 ALERT — "+Upper(s.Alert),alert.X+12f*scale,alert.Y+5f*scale,15f*scale,Color.FromArgb(255,255,197,80));
                }
            }
            catch(Exception ex){Game.LogTrivial("AdvancedK9 HUD render recovered safely: "+ex.Message);}
        }

        private static void DrawGlass(Rage.Graphics g,RectangleF r,int alpha){g.DrawRectangle(r,Color.FromArgb(230,35,215,235));g.DrawRectangle(new RectangleF(r.X+2,r.Y+2,r.Width-4,r.Height-4),Color.FromArgb(alpha,10,18,24));g.DrawRectangle(new RectangleF(r.X+4,r.Y+4,r.Width-8,1),Color.FromArgb(115,74,105,116));}
        private static void DrawMeter(Rage.Graphics g,string label,int value,float x,float y,float width,Color color,float scale){DrawText(g,label+"  "+value+"%",x,y,12f*scale,Color.FromArgb(255,225,239,242));float barY=y+17f*scale;g.DrawRectangle(new RectangleF(x,barY,width,4f*scale),Color.FromArgb(220,38,49,56));g.DrawRectangle(new RectangleF(x,barY,width*Math.Max(0,Math.Min(100,value))/100f,4f*scale),color);}
        private static void DrawText(Rage.Graphics g,string text,float x,float y,float size,Color color){if(string.IsNullOrWhiteSpace(text))return;g.DrawText(text,"Arial Narrow",size,new PointF(x,y),color);}
        private static string Upper(string value)=>(value??"").ToUpperInvariant();
        private static string Join(string current,string value)=>string.IsNullOrWhiteSpace(current)?value:current+"  •  "+value;
        private static string BreedBadge(string breed){if(string.IsNullOrWhiteSpace(breed))return "K9";string[] words=breed.Split(' ');return words.Length>1?(words[0][0].ToString()+words[1][0]).ToUpperInvariant():breed.Substring(0,Math.Min(2,breed.Length)).ToUpperInvariant();}
        private static Color StateColor(string state){return state!=null&&(state.IndexOf("search",StringComparison.OrdinalIgnoreCase)>=0||state.IndexOf("track",StringComparison.OrdinalIgnoreCase)>=0||state.IndexOf("appreh",StringComparison.OrdinalIgnoreCase)>=0)?Color.FromArgb(255,245,179,62):Color.FromArgb(255,65,222,138);}
        private void DisposePortrait(){var disposable=_portrait as IDisposable;if(disposable!=null)disposable.Dispose();_portrait=null;}
        public void Dispose(){if(_disposed)return;_disposed=true;Game.RawFrameRender-=Render;DisposePortrait();}
    }
}
