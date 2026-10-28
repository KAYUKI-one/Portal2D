using UnityEngine;

namespace Portal2D.Presentation.Utils
{
    /// <summary>
    /// 简单几何体与温暖森林主题精灵生成工具 (用于原型开发与免外部纹理依赖)
    /// </summary>
    public static class SpriteFactory
    {
        public static Sprite CreateBorderedBoxSprite(int size, Color fillColor, Color borderColor, int borderWidth = 2)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBorder = (x < borderWidth || x >= size - borderWidth ||
                                     y < borderWidth || y >= size - borderWidth);
                    texture.SetPixel(x, y, isBorder ? borderColor : fillColor);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        public static Sprite CreatePlatformSprite(int width, int height, Color color)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isTopEdge = (y >= height - 4);
                    texture.SetPixel(x, y, isTopEdge ? Color.white : color);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), width);
        }

        /// <summary>
        /// 森林主题快捷栏底槽精灵
        /// </summary>
        public static Sprite CreateForestSlotSprite(int size = 64, bool isSelected = false)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color bg = isSelected ? new Color(0.16f, 0.22f, 0.17f, 0.92f) : new Color(0.10f, 0.14f, 0.11f, 0.85f);
            Color border = isSelected ? new Color(0.96f, 0.82f, 0.44f, 1f) : new Color(0.33f, 0.45f, 0.34f, 0.8f);
            Color cornerHighlight = isSelected ? new Color(1f, 0.96f, 0.75f, 1f) : new Color(0.48f, 0.62f, 0.48f, 0.9f);

            int borderWidth = isSelected ? 3 : 2;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 倒角处理 (各角裁切 4 像素以呈现圆润外观)
                    bool isCornerCut = (x < 3 && y < 3) || (x >= size - 3 && y < 3) ||
                                       (x < 3 && y >= size - 3) || (x >= size - 3 && y >= size - 3);

                    if (isCornerCut)
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    bool isBorder = (x < borderWidth || x >= size - borderWidth ||
                                     y < borderWidth || y >= size - borderWidth);

                    if (isBorder)
                    {
                        bool isNearCorner = (x <= borderWidth + 2 || x >= size - borderWidth - 3) &&
                                            (y <= borderWidth + 2 || y >= size - borderWidth - 3);
                        texture.SetPixel(x, y, isNearCorner ? cornerHighlight : border);
                    }
                    else
                    {
                        // 细腻的内部渐变 (上方稍亮，呈现柔和环境光)
                        float vFactor = (float)y / size * 0.08f;
                        Color c = new Color(bg.r + vFactor, bg.g + vFactor, bg.b + vFactor, bg.a);
                        texture.SetPixel(x, y, c);
                    }
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 森林主题放置机关方块的世界精灵 (48x48，木纹浮雕与发光符文)
        /// </summary>
        public static Sprite CreateForestPuzzleBlockSprite(int size = 48)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color woodDark = new Color(0.36f, 0.23f, 0.13f, 1f);
            Color woodBase = new Color(0.55f, 0.36f, 0.22f, 1f);
            Color woodLight = new Color(0.68f, 0.46f, 0.28f, 1f);
            Color runeGlow = new Color(0.48f, 0.85f, 0.42f, 1f);

            int center = size / 2;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isOuterBorder = (x < 2 || x >= size - 2 || y < 2 || y >= size - 2);
                    bool isBevelTopLeft = (x < 4 || y >= size - 4);
                    bool isBevelBottomRight = (x >= size - 4 || y < 4);

                    // 居中菱形自然符文
                    int dx = Mathf.Abs(x - center);
                    int dy = Mathf.Abs(y - center);
                    bool isRune = (dx + dy == 6) || (dx + dy == 7) || (dx == 0 && dy <= 4) || (dy == 0 && dx <= 4);

                    if (isOuterBorder)
                    {
                        texture.SetPixel(x, y, woodDark);
                    }
                    else if (isRune)
                    {
                        texture.SetPixel(x, y, runeGlow);
                    }
                    else if (isBevelTopLeft)
                    {
                        texture.SetPixel(x, y, woodLight);
                    }
                    else if (isBevelBottomRight)
                    {
                        texture.SetPixel(x, y, woodDark);
                    }
                    else
                    {
                        // 木纹横线
                        bool grain = (y % 8 == 0 || y % 8 == 1);
                        texture.SetPixel(x, y, grain ? woodDark : woodBase);
                    }
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        /// <summary>
        /// 生成各种背包道具专属的高清图标 (36x36)
        /// </summary>
        public static Sprite CreateItemIconSprite(string itemId, int size = 36)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    texture.SetPixel(x, y, Color.clear);

            int cx = size / 2;
            int cy = size / 2;

            switch (itemId)
            {
                case "PortalGun":
                    // 传送门枪：青铜外壳 + 枪管 + 发光青蓝核心
                    Color gunMetal = new Color(0.35f, 0.38f, 0.42f, 1f);
                    Color gunBrass = new Color(0.78f, 0.55f, 0.28f, 1f);
                    Color cyanCore = new Color(0.25f, 0.92f, 0.96f, 1f);

                    for (int y = 10; y <= 24; y++)
                    {
                        for (int x = 8; x <= 28; x++)
                        {
                            // 枪身轮廓
                            if (x >= 12 && x <= 24 && y >= 14 && y <= 22)
                                texture.SetPixel(x, y, gunBrass);
                            // 握把
                            else if (x >= 10 && x <= 14 && y >= 8 && y <= 16)
                                texture.SetPixel(x, y, gunMetal);
                            // 前段枪口与爪叉
                            else if (x >= 24 && x <= 29 && (y == 20 || y == 16))
                                texture.SetPixel(x, y, gunMetal);
                            // 发光核心
                            if (x >= 16 && x <= 20 && y >= 16 && y <= 20)
                                texture.SetPixel(x, y, cyanCore);
                        }
                    }
                    break;

                case "PuzzleBlock":
                    // 机关方块：精巧微缩木箱 + 绿光符文
                    Color bDark = new Color(0.36f, 0.23f, 0.13f, 1f);
                    Color bLight = new Color(0.65f, 0.44f, 0.26f, 1f);
                    Color bRune = new Color(0.48f, 0.88f, 0.45f, 1f);

                    for (int y = 7; y <= 28; y++)
                    {
                        for (int x = 7; x <= 28; x++)
                        {
                            bool border = (x == 7 || x == 28 || y == 7 || y == 28);
                            int dx = Mathf.Abs(x - cx);
                            int dy = Mathf.Abs(y - cy);
                            bool rune = (dx + dy <= 3);

                            if (border) texture.SetPixel(x, y, bDark);
                            else if (rune) texture.SetPixel(x, y, bRune);
                            else texture.SetPixel(x, y, bLight);
                        }
                    }
                    break;

                case "Key":
                    // 古铜/黄金钥匙：环形手柄 + 钥匙轴与齿
                    Color gold = new Color(0.95f, 0.78f, 0.25f, 1f);
                    Color goldShade = new Color(0.72f, 0.54f, 0.15f, 1f);

                    // 顶部钥匙环
                    for (int y = 18; y <= 29; y++)
                    {
                        for (int x = 11; x <= 24; x++)
                        {
                            float dist = Vector2.Distance(new Vector2(x, y), new Vector2(17.5f, 23.5f));
                            if (dist <= 5.5f && dist >= 2.5f)
                            {
                                texture.SetPixel(x, y, gold);
                            }
                        }
                    }
                    // 钥匙柄长条
                    for (int y = 7; y <= 19; y++)
                    {
                        for (int x = 16; x <= 18; x++)
                        {
                            texture.SetPixel(x, y, gold);
                        }
                    }
                    // 齿端
                    for (int y = 7; y <= 12; y++)
                    {
                        for (int x = 19; x <= 23; x++)
                        {
                            if (y == 7 || y == 8 || y == 11 || y == 12)
                                texture.SetPixel(x, y, goldShade);
                        }
                    }
                    break;

                case "Seeds":
                    // 自然种子：三颗散落的翡翠嫩叶萌芽种子
                    Color seedGreen = new Color(0.38f, 0.82f, 0.45f, 1f);
                    Color seedWarm = new Color(0.85f, 0.78f, 0.35f, 1f);

                    // 种子 1 (主中央)
                    for (int y = 12; y <= 24; y++)
                    {
                        for (int x = 13; x <= 22; x++)
                        {
                            float d = Vector2.Distance(new Vector2(x, y), new Vector2(17.5f, 17f));
                            if (d <= 4.2f)
                            {
                                texture.SetPixel(x, y, (x <= 17) ? seedGreen : seedWarm);
                            }
                        }
                    }
                    // 叶尖萌芽
                    texture.SetPixel(17, 23, seedGreen);
                    texture.SetPixel(18, 24, seedGreen);
                    texture.SetPixel(19, 25, seedGreen);
                    break;

                default:
                    // 兜底小宝箱/物品
                    Color defColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                    for (int y = 10; y <= 25; y++)
                    {
                        for (int x = 10; x <= 25; x++)
                        {
                            texture.SetPixel(x, y, defColor);
                        }
                    }
                    break;
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite _maskSprite;
        /// <summary>
        /// 获取或创建用于 SpriteMask 视觉遮罩的纯白矩形精灵
        /// </summary>
        public static Sprite GetMaskSprite()
        {
            if (_maskSprite == null)
            {
                var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                        tex.SetPixel(x, y, Color.white);
                tex.Apply();
                _maskSprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32);
            }
            return _maskSprite;
        }
    }
}
