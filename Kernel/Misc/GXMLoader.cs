using System;
using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.OS;

namespace guideXOS.Misc {
    // Minimal loader for GXM (formerly MUE) single-image executables.
    // Layout: [0..3] 'G','X','M','\0' (or legacy 'M','U','E'+'\0')
    //         [4..7]  version (u32)
    //         [8..11] entry RVA (u32)
    //         [12..15] image size (u32)
    //         [16..]  raw image
    public static unsafe class GXMLoader {
        public static bool TryExecute(byte[] image, out string error) {
            return TryExecute(image, out error, null);
        }

        public static bool TryExecute(byte[] image, out string error,
                                      ApplicationInstance instance) {
            error = null; if (image == null || image.Length < 16) { error = "Executable too small"; return false; }
            byte b0 = image[0], b1 = image[1], b2 = image[2], b3 = image[3];
            bool sigGXM = (b0=='G' && b1=='X' && b2=='M' && b3==0);
            bool sigMUE = (b0=='M' && b1=='U' && b2=='E' && b3==0);
            if (!sigGXM && !sigMUE) { error = "Bad signature"; return false; }
            uint ver = ReadU32(image, 4);
            uint entryRva = ReadU32(image, 8);
            uint size = ReadU32(image, 12);
            if (size > (uint)image.Length) size = (uint)image.Length; if (entryRva >= size || size < 16) { error = "Invalid header"; return false; }

            // New: Optional GUI script preface after header.
            // If the bytes at [16..19] equal 'G','U','I'+'\0', then from [20..] until a double-NUL sequence is a UTF-8 script.
            if (size >= 20 && image[16]=='G' && image[17]=='U' && image[18]=='I' && image[19]==0) {
                int pos = 20; int end = (int)size; var win = new GXMScriptWindow("Script", 420, 300);
                if (instance != null &&
                    !ApplicationInstanceRegistry.TryAttachWindow(instance, win)) {
                    win.CloseForApplicationTermination();
                    error = "Application instance window capacity exhausted";
                    return false;
                }
                // parse lines separated by \n (0x0A), fields with '|'
                int lineStart = pos;
                int safetyCounter = 0; // Prevent infinite loops
                const int maxIterations = 100; // REDUCED: Scripts should be tiny (10-20 lines max)
                while (pos < end && safetyCounter < maxIterations) {
                    safetyCounter++;
                    byte c = pos < end ? image[pos] : (byte)0;
                    pos++;
                    
                    if (c == 0 || c == (byte)('\n') || c == (byte)('\r')) {
                        int len = pos - lineStart - 1; 
                        if (len > 0) { 
                            string line = ExtractUtf8(image, lineStart, len);
                            ApplyGuiLine(win, line);
                            line.Dispose(); // CRITICAL: Dispose the line to prevent memory leak
                        }
                        if (c == 0) break; // Stop at NUL terminator
                        
                        // Skip additional CR/LF characters (with safety limit)
                        int skipCounter = 0;
                        while (pos < end && skipCounter < 10 && (image[pos] == (byte)('\n') || image[pos] == (byte)('\r'))) {
                            pos++;
                            skipCounter++;
                        }
                        
                        lineStart = pos;
                    }
                }
                // show window and return
                WindowManager.MoveToEnd(win); win.Visible = true;
#if UEFI_DIAGNOSTIC_APP_RUNTIME
                Program.MarkUefiAppRuntime("GXM_INSTANCE_WINDOW_BOUNDS=x=" +
                    win.X.ToString() + ";y=" + win.Y.ToString() +
                    ";w=" + win.Width.ToString() + ";h=" +
                    win.Height.ToString() + ";instance=" +
                    (instance == null ? "" : instance.Handle.ToString()));
#endif
                return true;
            }

            ulong allocSize = AlignUp(size, 4096);
            byte* basePtr = (byte*)Allocator.Allocate(allocSize); if (basePtr == null) { error = "OOM"; return false; }
            fixed (byte* src = image) Native.Movsb(basePtr, src, size);
            PageTable.MapUser((ulong)basePtr, (ulong)basePtr);
            for (ulong off = 4096; off < allocSize; off += 4096) PageTable.MapUser((ulong)basePtr + off, (ulong)basePtr + off);
            const ulong StackSize = 64 * 1024; byte* stack = (byte*)Allocator.Allocate(StackSize); if (stack == null) { error = "OOM stack"; return false; }
            PageTable.MapUser((ulong)stack, (ulong)stack);
            for (ulong off = 4096; off < StackSize; off += 4096) PageTable.MapUser((ulong)stack + off, (ulong)stack + off);
            ulong rsp = (ulong)stack + StackSize - 16; ulong rip = (ulong)basePtr + entryRva; SchedulerExtensions.EnterUserMode(rip, rsp); return true;
        }
        private static void ApplyGuiLine(GXMScriptWindow win, string line){ 
            if(line==null||line.Length==0) return; 
            int p0=IndexOf(line,'|',0); 
            if(p0==-1) return; 
            string cmd=line.Substring(0,p0); 
            string rest=line.Substring(p0+1);
            
            if(StringEquals(cmd,"WINDOW")) { 
                int p1=IndexOf(rest,'|',0); 
                if(p1!=-1) {
                    string title=rest.Substring(0,p1); 
                    string wh=rest.Substring(p1+1); 
                    int p2=IndexOf(wh,'|',0); 
                    if(p2!=-1) {
                        string ws=wh.Substring(0,p2);
                        string hs=wh.Substring(p2+1);
                        int w=ToInt(ws); int h=ToInt(hs); 
                        win.Title=title; win.Width=w>160?w:160; win.Height=h>120?h:120; 
                        win.X=(Framebuffer.Width-win.Width)/2; win.Y=(Framebuffer.Height-win.Height)/2;
                        // Don't dispose title - it's now owned by window
                        wh.Dispose(); ws.Dispose(); hs.Dispose();
                    } else {
                        title.Dispose(); wh.Dispose();
                    }
                }
            }
            else if(StringEquals(cmd,"RESIZABLE")) { win.IsResizable = ToBool(rest); }
            else if(StringEquals(cmd,"TASKBAR")) { win.ShowInTaskbar = ToBool(rest); }
            else if(StringEquals(cmd,"MAXIMIZE")) { win.ShowMaximize = ToBool(rest); }
            else if(StringEquals(cmd,"MINIMIZE")) { win.ShowMinimize = ToBool(rest); }
            else if(StringEquals(cmd,"TOMBSTONE")) { win.ShowTombstone = ToBool(rest); }
            else if(StringEquals(cmd,"STARTMENU")) { win.ShowInStartMenu = ToBool(rest); }
            else if(StringEquals(cmd,"TEXTBOX")) { 
                int i0=NextField(rest,0,out string f0); int id=ToInt(f0); f0.Dispose();
                int i1=NextField(rest,i0,out string f1); int x=ToInt(f1); f1.Dispose();
                int i2=NextField(rest,i1,out string f2); int y=ToInt(f2); f2.Dispose();
                int i3=NextField(rest,i2,out string f3); int w=ToInt(f3); f3.Dispose();
                int i4=NextField(rest,i3,out string f4); int h=ToInt(f4); f4.Dispose();
                string f5 = "";
                if(i4 < rest.Length) { NextField(rest,i4,out f5); }
                win.AddTextBox(id,x,y,w,h,f5);
                if(f5.Length > 0) f5.Dispose();
            }
            else if(StringEquals(cmd,"BUTTON")) { 
                int i0=NextField(rest,0,out string f0); int id=ToInt(f0); f0.Dispose();
                int i1=NextField(rest,i0,out string f1); string text=f1; // Keep for button
                int i2=NextField(rest,i1,out string f2); int x=ToInt(f2); f2.Dispose();
                int i3=NextField(rest,i2,out string f3); int y=ToInt(f3); f3.Dispose();
                int i4=NextField(rest,i3,out string f4); int w=ToInt(f4); f4.Dispose();
                NextField(rest,i4,out string f5); int h=ToInt(f5); f5.Dispose();
                win.AddButton(id,text,x,y,w,h); 
            }
            else if(StringEquals(cmd,"LABEL")) { 
                int i0=NextField(rest,0,out string f0); string text=f0; // Keep for label
                int i1=NextField(rest,i0,out string f1); int x=ToInt(f1); f1.Dispose();
                NextField(rest,i1,out string f2); int y=ToInt(f2); f2.Dispose();
                win.AddLabel(text,x,y); 
            }
            else if(StringEquals(cmd,"LIST")) { 
                int i0=NextField(rest,0,out string f0); int id=ToInt(f0); f0.Dispose();
                int i1=NextField(rest,i0,out string f1); int x=ToInt(f1); f1.Dispose();
                int i2=NextField(rest,i1,out string f2); int y=ToInt(f2); f2.Dispose();
                int i3=NextField(rest,i2,out string f3); int w=ToInt(f3); f3.Dispose();
                int i4=NextField(rest,i3,out string f4); int h=ToInt(f4); f4.Dispose();
                NextField(rest,i4,out string f5); 
                win.AddList(id,x,y,w,h,f5);
                f5.Dispose();
            }
            else if(StringEquals(cmd,"DROPDOWN")) { 
                int i0=NextField(rest,0,out string f0); int id=ToInt(f0); f0.Dispose();
                int i1=NextField(rest,i0,out string f1); int x=ToInt(f1); f1.Dispose();
                int i2=NextField(rest,i1,out string f2); int y=ToInt(f2); f2.Dispose();
                int i3=NextField(rest,i2,out string f3); int w=ToInt(f3); f3.Dispose();
                int i4=NextField(rest,i3,out string f4); int h=ToInt(f4); f4.Dispose();
                NextField(rest,i4,out string f5); 
                win.AddDropdown(id,x,y,w,h,f5);
                f5.Dispose();
            }
            else if(StringEquals(cmd,"ONCLICK")) { 
                int i0=NextField(rest,0,out string f0); int id=ToInt(f0); f0.Dispose();
                int i1=NextField(rest,i0,out string f1); string action=f1; // Keep for callback
                NextField(rest,i1,out string f2); string arg=f2; // Keep for callback
                win.AddOnClick(id, action, arg);
            }
            else if(StringEquals(cmd,"ONCHANGE")) { 
                int i0=NextField(rest,0,out string f0); int id=ToInt(f0); f0.Dispose();
                int i1=NextField(rest,i0,out string f1); string action=f1;
                NextField(rest,i1,out string f2); string arg=f2;
                win.AddOnChange(id, action, arg);
            }
            else if(StringEquals(cmd,"ONTEXTCHANGE")) { 
                int i0=NextField(rest,0,out string f0); int id=ToInt(f0); f0.Dispose();
                int i1=NextField(rest,i0,out string f1); string action=f1;
                NextField(rest,i1,out string f2); string arg=f2;
                win.AddOnTextChange(id, action, arg);
            }
            
            // Always dispose cmd and rest at the end
            cmd.Dispose();
            rest.Dispose();
        }
        private static int NextField(string s,int start,out string field){ int i=IndexOf(s,'|',start); if(i==-1){ field=s.Substring(start); return s.Length; } field=s.Substring(start,i-start); return i+1; }
        private static int IndexOf(string s,char c,int start){ for(int i=start;i<s.Length;i++){ if(s[i]==c) return i; } return -1; }
        private static bool StringEquals(string a,string b){ if(a==null||b==null||a.Length!=b.Length) return false; for(int i=0;i<a.Length;i++){ char ca=a[i]; char cb=b[i]; if(ca>=65&&ca<=90) ca=(char)(ca+32); if(cb>=65&&cb<=90) cb=(char)(cb+32); if(ca!=cb) return false; } return true; }
        private static int ToInt(string s){ int n=0; bool neg=false; if(!string.IsNullOrEmpty(s)){ int i=0; if(s[0]=='-'){ neg=true; i=1; } for(;i<s.Length;i++){ char ch=s[i]; if(ch<'0'||ch>'9') break; n=n*10+(ch-'0'); } } return neg?-n:n; }
        private static bool ToBool(string s){ 
            if(string.IsNullOrEmpty(s)) return false;
            int len = s.Length;
            // Fast path for common cases without allocations
            if(len == 1) {
                char c = s[0];
                return c == '1';
            }
            if(len == 2) {
                char c0 = s[0]; char c1 = s[1];
                if(c0 >= 'A' && c0 <= 'Z') c0 = (char)(c0 + 32);
                if(c1 >= 'A' && c1 <= 'Z') c1 = (char)(c1 + 32);
                return c0 == 'o' && c1 == 'n';
            }
            if(len == 3) {
                char c0 = s[0]; char c1 = s[1]; char c2 = s[2];
                if(c0 >= 'A' && c0 <= 'Z') c0 = (char)(c0 + 32);
                if(c1 >= 'A' && c1 <= 'Z') c1 = (char)(c1 + 32);
                if(c2 >= 'A' && c2 <= 'Z') c2 = (char)(c2 + 32);
                return c0 == 'y' && c1 == 'e' && c2 == 's';
            }
            if(len == 4) {
                char c0 = s[0]; char c1 = s[1]; char c2 = s[2]; char c3 = s[3];
                if(c0 >= 'A' && c0 <= 'Z') c0 = (char)(c0 + 32);
                if(c1 >= 'A' && c1 <= 'Z') c1 = (char)(c1 + 32);
                if(c2 >= 'A' && c2 <= 'Z') c2 = (char)(c2 + 32);
                if(c3 >= 'A' && c3 <= 'Z') c3 = (char)(c3 + 32);
                return c0 == 't' && c1 == 'r' && c2 == 'u' && c3 == 'e';
            }
            return false;
        }
        private static string ExtractUtf8(byte[] b,int off,int len){ 
            char[] ch=new char[len]; 
            for(int i=0;i<len;i++) ch[i]=(char)b[off+i]; 
            string result = new string(ch);
            ch.Dispose(); // CRITICAL: Dispose char array to prevent memory leak
            return result; 
        }
        private static uint ReadU32(byte[] b, int off){ return (uint)(b[off] | (b[off+1]<<8) | (b[off+2]<<16) | (b[off+3]<<24)); }
        private static ulong AlignUp(uint v, uint a){ uint r = (v + a - 1) & ~(a - 1); return r; }
    }
}
