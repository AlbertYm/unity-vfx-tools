#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EffectTools.TextureAdjust
{
    public static class TexturePixels
    {
        public static Color32[] Process(Color32[] src, int w, int h, float angle, bool expand,
            bool gray, float exposure, float contrast, float black, float white, float gamma,
            out int ow, out int oh)
        {
            double radians = angle * Math.PI / 180.0;
            float c = (float)Math.Cos(radians), s = (float)Math.Sin(radians);
            ow = expand ? Mathf.CeilToInt(Mathf.Abs(w*c)+Mathf.Abs(h*s)-0.0001f) : w;
            oh = expand ? Mathf.CeilToInt(Mathf.Abs(w*s)+Mathf.Abs(h*c)-0.0001f) : h;
            ow = Mathf.Max(1, ow); oh = Mathf.Max(1, oh);
            if ((long)ow*oh > 33554432 || ow > 16384 || oh > 16384)
                throw new InvalidOperationException("输出过大：请保持原尺寸或使用较小的源图（上限 3200 万像素）。");
            var dst = new Color32[ow*oh];
            bool neutral = !gray && exposure == 0 && contrast == 1 && black == 0 && white == 1 && gamma == 1;
            float gain = Mathf.Pow(2, exposure);
            for (int y=0; y<oh; y++) for (int x=0; x<ow; x++)
            {
                float dx=x-(ow-1)*0.5f, dy=y-(oh-1)*0.5f;
                float sx=c*dx-s*dy+(w-1)*0.5f, sy=s*dx+c*dy+(h-1)*0.5f;
                Color col;
                int ix=Mathf.RoundToInt(sx), iy=Mathf.RoundToInt(sy);
                if (Mathf.Abs(sx-ix)<0.0001f && Mathf.Abs(sy-iy)<0.0001f)
                    col = ix>=0 && ix<w && iy>=0 && iy<h ? (Color)src[iy*w+ix] : Color.clear;
                else
                {
                    int bx=Mathf.FloorToInt(sx), by=Mathf.FloorToInt(sy);
                    float tx=sx-bx, ty=sy-by;
                    col=Color.clear;
                    for(int j=0;j<2;j++) for(int i=0;i<2;i++)
                    {
                        int px=bx+i, py=by+j;
                        if(px<0 || px>=w || py<0 || py>=h) continue;
                        Color a=src[py*w+px];
                        float weight=(i==0?1-tx:tx)*(j==0?1-ty:ty);
                        col.r+=a.r*a.a*weight; col.g+=a.g*a.a*weight; col.b+=a.b*a.a*weight; col.a+=a.a*weight;
                    }
                    if(col.a>0.000001f) { col.r/=col.a; col.g/=col.a; col.b/=col.a; }
                }
                if(!neutral)
                {
                    if(gray) col.r=col.g=col.b=col.r*0.2126f+col.g*0.7152f+col.b*0.0722f;
                    col.r=Tone(col.r,gain,contrast,black,white,gamma);
                    col.g=Tone(col.g,gain,contrast,black,white,gamma);
                    col.b=Tone(col.b,gain,contrast,black,white,gamma);
                }
                dst[y*ow+x]=col;
            }
            return dst;
        }
        static float Tone(float v,float gain,float contrast,float black,float white,float gamma)
        {
            v=Mathf.Clamp01((v*gain-black)/Mathf.Max(0.001f,white-black));
            return Mathf.Clamp01((Mathf.Pow(v,1/Mathf.Max(0.01f,gamma))-0.5f)*contrast+0.5f);
        }
    }

    public sealed class VFXTextureAdjust : EditorWindow
    {
        Texture2D source, original, preview;
        Color32[] previewPixels;
        float angle, exposure, contrast=1, black, white=1, gamma=1;
        bool expand=true, gray, dirty;
        int background;
        string info="请选择 Project 中的贴图。", error="";
        Vector2 scroll;
        double changedAt;
        [MenuItem("Effect Tools/Texture Adjust/贴图调整")]
        public static void Open()
        {
            var window=GetWindow<VFXTextureAdjust>("贴图调整");
            window.minSize=new Vector2(560,650);
            if(Selection.activeObject is Texture2D texture) window.Load(texture);
        }
        [MenuItem("Assets/Effect Tools/贴图调整", false, 2100)]
        static void OpenSelected() { Open(); }
        [MenuItem("Assets/Effect Tools/贴图调整", true)]
        static bool ValidSelection() { return Selection.activeObject is Texture2D; }
        void OnEnable() { EditorApplication.update+=Tick; }
        void OnDisable()
        {
            EditorApplication.update-=Tick;
            DestroyImmediate(original); DestroyImmediate(preview);
        }
        void Tick()
        {
            if(!dirty || EditorApplication.timeSinceStartup-changedAt<0.12) return;
            dirty=false;
            try
            {
                if(original==null) return;
                int pw,ph;
                var pixels=TexturePixels.Process(previewPixels,original.width,original.height,angle,expand,gray,exposure,contrast,black,white,gamma,out pw,out ph);
                DestroyImmediate(preview);
                preview=new Texture2D(pw,ph,TextureFormat.RGBA32,false);
                preview.hideFlags=HideFlags.HideAndDontSave;
                preview.SetPixels32(pixels); preview.Apply(); error="";
            }
            catch(Exception e) { error=e.Message; }
            Repaint();
        }
        void MarkDirty() { dirty=true; changedAt=EditorApplication.timeSinceStartup; }
        void ResetAdjustments() { angle=exposure=black=0; contrast=white=gamma=1; gray=false; expand=true; MarkDirty(); }
        void Load(Texture2D texture)
        {
            source=texture; DestroyImmediate(original); original=null; DestroyImmediate(preview); preview=null;
            previewPixels=null; error="";
            if(source==null) return;
            try
            {
                var full=ReadSource(source, out string readInfo);
                try
                {
                    info=readInfo+"；"+full.width+" × "+full.height+"。预览最长边 640，导出使用完整读取尺寸。";
                    original=ResizePreview(full); previewPixels=original.GetPixels32();
                }
                finally { DestroyImmediate(full); }
                ResetAdjustments();
            }
            catch(Exception e) { error=e.Message; }
        }
        public static Texture2D ReadSource(Texture2D texture, out string mode)
        {
            string path=AssetDatabase.GetAssetPath(texture);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null || (importer.textureType!=TextureImporterType.Default && importer.textureType!=TextureImporterType.Sprite))
                throw new InvalidOperationException("请选择普通颜色贴图或 Sprite；不处理法线图、光照图和其他数据贴图。");
            string ext=Path.GetExtension(path).ToLowerInvariant();
            if(ext==".png" || ext==".jpg" || ext==".jpeg")
            {
                var result=new Texture2D(2,2,TextureFormat.RGBA32,false);
                result.hideFlags=HideFlags.HideAndDontSave;
                try
                {
                    if(!ImageConversion.LoadImage(result,File.ReadAllBytes(path))) throw new IOException("图片解码失败。");
                    mode="直接读取原始 PNG/JPG"; return result;
                }
                catch { DestroyImmediate(result); throw; }
            }
            mode="读取 Unity 导入后的像素（尺寸、压缩受当前导入设置影响）";
            return CopyGPU(texture,texture.width,texture.height,importer.sRGBTexture);
        }
        static Texture2D ResizePreview(Texture2D full)
        {
            float scale=Mathf.Min(1,640f/Mathf.Max(full.width,full.height));
            return CopyGPU(full,Mathf.Max(1,Mathf.RoundToInt(full.width*scale)),Mathf.Max(1,Mathf.RoundToInt(full.height*scale)),true);
        }
        static Texture2D CopyGPU(Texture source,int w,int h,bool srgb)
        {
            var rt=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32,srgb?RenderTextureReadWrite.sRGB:RenderTextureReadWrite.Linear);
            var previous=RenderTexture.active; bool oldWrite=GL.sRGBWrite;
            Texture2D result=null;
            try
            {
                GL.sRGBWrite=srgb && QualitySettings.activeColorSpace==ColorSpace.Linear;
                Graphics.Blit(source,rt); RenderTexture.active=rt;
                result=new Texture2D(w,h,TextureFormat.RGBA32,false,!srgb);
                result.hideFlags=HideFlags.HideAndDontSave;
                result.ReadPixels(new Rect(0,0,w,h),0,0); result.Apply(); return result;
            }
            catch { DestroyImmediate(result); throw; }
            finally { GL.sRGBWrite=oldWrite; RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); }
        }
        void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("特效贴图调整",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("编辑器内烘焙新 PNG，不改 Shader，不改原图。颜色调整保留 Alpha；任意角度旋转会重新采样。",MessageType.Info);
            var selected=(Texture2D)EditorGUILayout.ObjectField("源贴图",source,typeof(Texture2D),false);
            if(selected!=source) Load(selected);
            if(GUILayout.Button("使用 Project 当前选中的贴图") && Selection.activeObject is Texture2D t) Load(t);
            EditorGUILayout.LabelField(info,EditorStyles.wordWrappedMiniLabel);
            EditorGUI.BeginChangeCheck();
            angle=EditorGUILayout.Slider("旋转角度（正值顺时针）",angle,-180,180);
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("↶ 90°")) { angle=Mathf.Repeat(angle-90+180,360)-180; MarkDirty(); }
            if(GUILayout.Button("↷ 90°")) { angle=Mathf.Repeat(angle+90+180,360)-180; MarkDirty(); }
            if(GUILayout.Button("180°")) { angle=Mathf.Repeat(angle+360,360)-180; MarkDirty(); }
            EditorGUILayout.EndHorizontal();
            expand=EditorGUILayout.Toggle("扩展画布（避免裁切）",expand);
            gray=EditorGUILayout.Toggle("去色（黑白灰）",gray);
            exposure=EditorGUILayout.Slider("明度 / 曝光（EV）",exposure,-3,3);
            contrast=EditorGUILayout.Slider("对比度",contrast,0,3);
            black=EditorGUILayout.Slider("黑场（提高则暗部更黑）",black,0,Mathf.Max(0,white-0.001f));
            white=EditorGUILayout.Slider("白场（降低则亮部更白）",white,black+0.001f,1);
            gamma=EditorGUILayout.Slider("中间调（提高则提亮）",gamma,0.1f,3);
            if(EditorGUI.EndChangeCheck()) MarkDirty();
            background=EditorGUILayout.Popup("预览底色（不写入图片）",background,new[]{"棋盘格","黑色","白色"});
            if(GUILayout.Button("重置全部调整")) ResetAdjustments();
            EditorGUILayout.BeginHorizontal(); GUILayout.Label("原图",EditorStyles.boldLabel); GUILayout.Label(dirty?"效果（更新中…）":"调整效果",EditorStyles.boldLabel); EditorGUILayout.EndHorizontal();
            Rect row=GUILayoutUtility.GetRect(100,Mathf.Clamp(position.width*0.42f,180,350),GUILayout.ExpandWidth(true));
            DrawPreview(new Rect(row.x,row.y,(row.width-8)/2,row.height),original);
            DrawPreview(new Rect(row.x+(row.width+8)/2,row.y,(row.width-8)/2,row.height),preview);
            if(!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error,MessageType.Error);
            using(new EditorGUI.DisabledScope(original==null))
                if(GUILayout.Button("另存为新 PNG",GUILayout.Height(34))) Export();
            EditorGUILayout.EndScrollView();
        }
        void DrawPreview(Rect rect,Texture2D texture)
        {
            EditorGUI.DrawRect(rect,background==2?Color.white:new Color(.13f,.13f,.13f));
            if(background==0)
                for(float y=rect.y;y<rect.yMax;y+=16) for(float x=rect.x;x<rect.xMax;x+=16)
                    if(((int)((x-rect.x)/16)+(int)((y-rect.y)/16))%2==0)
                        EditorGUI.DrawRect(new Rect(x,y,Mathf.Min(16,rect.xMax-x),Mathf.Min(16,rect.yMax-y)),new Color(.22f,.22f,.22f));
            if(texture!=null) GUI.DrawTexture(rect,texture,ScaleMode.ScaleToFit,true);
        }
        void Export()
        {
            string sourcePath=AssetDatabase.GetAssetPath(source);
            string folder=Path.GetDirectoryName(sourcePath).Replace('\\','/');
            if(!folder.StartsWith("Assets",StringComparison.Ordinal)) folder="Assets";
            string path=EditorUtility.SaveFilePanelInProject("另存为新贴图",source.name+"_Adjusted","png","选择保存位置；已有文件会自动加编号，不会覆盖。",folder);
            if(string.IsNullOrEmpty(path)) return;
            path=AssetDatabase.GenerateUniqueAssetPath(path);
            Texture2D full=null,output=null;
            try
            {
                EditorUtility.DisplayProgressBar("贴图调整","读取并处理完整尺寸像素…",0.2f);
                full=ReadSource(source,out _);
                int w,h;
                var pixels=TexturePixels.Process(full.GetPixels32(),full.width,full.height,angle,expand,gray,exposure,contrast,black,white,gamma,out w,out h);
                output=new Texture2D(w,h,TextureFormat.RGBA32,false);
                output.SetPixels32(pixels); output.Apply();
                File.WriteAllBytes(path,output.EncodeToPNG());
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var src=(TextureImporter)AssetImporter.GetAtPath(sourcePath);
                var dst=(TextureImporter)AssetImporter.GetAtPath(path);
                dst.textureType=TextureImporterType.Default;
                dst.sRGBTexture=src.sRGBTexture; dst.alphaSource=TextureImporterAlphaSource.FromInput;
                dst.alphaIsTransparency=src.alphaIsTransparency; dst.mipmapEnabled=src.mipmapEnabled;
                dst.wrapModeU=src.wrapModeU; dst.wrapModeV=src.wrapModeV;
                dst.filterMode=src.filterMode; dst.anisoLevel=src.anisoLevel;
                dst.maxTextureSize=Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(w,h)),32,16384);
                dst.npotScale=TextureImporterNPOTScale.None;
                dst.textureCompression=TextureImporterCompression.Uncompressed;
                dst.isReadable=false; dst.SaveAndReimport();
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(path));
                ShowNotification(new GUIContent("已保存："+path)); error="";
            }
            catch(Exception e) { error="导出失败："+e.Message; Debug.LogException(e); }
            finally { DestroyImmediate(full); DestroyImmediate(output); EditorUtility.ClearProgressBar(); }
        }
    }
}
#endif
