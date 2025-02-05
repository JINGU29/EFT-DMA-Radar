using SkiaSharp;
using System;
using System.IO;

namespace eft_dma_radar
{
    public static class FontHelper
    {
        private static SKTypeface _chineseTypeface;
        
        public static SKTypeface ChineseTypeface
        {
            get
            {
                if (_chineseTypeface == null)
                {
                    // 先尝试从文件加载字体
                    string fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts", "Microsoft YaHei.ttf");
                    if (File.Exists(fontPath))
                    {
                        _chineseTypeface = SKTypeface.FromFile(fontPath);
                    }
                    
                    // 如果文件加载失败，尝试系统字体
                    if (_chineseTypeface == null)
                    {
                        var fonts = new[] { "Microsoft YaHei", "SimHei", "SimSun", "NSimSun" };
                        foreach (var font in fonts)
                        {
                            _chineseTypeface = SKTypeface.FromFamilyName(font);
                            if (_chineseTypeface != null) break;
                        }
                    }
                    
                    // 如果还是失败，使用默认字体
                    if (_chineseTypeface == null)
                    {
                        _chineseTypeface = SKTypeface.Default;
                    }
                }
                return _chineseTypeface;
            }
        }
    }
} 