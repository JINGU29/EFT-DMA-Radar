using System.Numerics;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Linq;

namespace eft_dma_radar
{
    public class ESP
    {
        private readonly Config config;
        private const float AIMVIEW_OBJECT_SPACING = 4f;
        private float uiScale = 1.0f;
        private readonly ESPSystem _espSystem;

        public ESP(Config config)
        {
            this.config = config;
            _espSystem = new ESPSystem(config);
        }

        public void SetUIScale(float scale)
        {
            this.uiScale = scale;
        }

        public void DrawPlayer(SKCanvas canvas, Player player, Vector2 screenPos, float distance, AimviewObjectSettings objectSettings, Player localPlayer)
        {
            if (distance >= Math.Max(objectSettings.PaintDistance, objectSettings.TextDistance))
                return;

            // プレイヤータイプごとの設定を取得
            var typeSettings = GetPlayerTypeSettings(player.Type);

            // 直接ペイントを取得
            var paint = player.GetAimviewPaint();
            Vector2 textPosition = screenPos;
            bool isWithinPaintDistance = distance < objectSettings.PaintDistance;
            bool isWithinTextDistance = distance < objectSettings.TextDistance;

            // プレイヤーが自分を狙っているかチェックし、警告を表示
            DrawTargetingWarning(canvas, player, localPlayer, paint, typeSettings);

            if (isWithinPaintDistance)
            {
                switch (typeSettings.ESPStyle)
                {
                    case ESPStyle.Skeleton:
                        DrawPlayerSkeleton(player, canvas);
                        break;
                    case ESPStyle.Box:
                        textPosition = DrawPlayerBox(canvas, player, distance, objectSettings);
                        if (textPosition == Vector2.Zero)
                            return;
                        break;
                    case ESPStyle.Dot:
                        DrawPlayerDot(canvas, player, screenPos, distance, objectSettings);
                        break;
                }
            }

            if (isWithinTextDistance)
            {
                DrawPlayerTextInfo(canvas, player, distance, textPosition, objectSettings, typeSettings);
            }
        }

        public void DrawTargetingWarning(SKCanvas canvas, Player player, Player localPlayer, SKPaint basePaint, PlayerTypeSettings typeSettings)
        {
            if (localPlayer == null || !player.IsTargetingLocalPlayer || !typeSettings.ShowTargetedAlert)
                return;

            var bounds = GetAimviewBounds(canvas);
            using var warningPaint = new SKPaint
            {
                Color = player.IsTargetingLocalPlayer ? basePaint.Color.WithAlpha(127) : SKColors.Transparent,
                StrokeWidth = 30,
                Style = SKPaintStyle.Stroke
            };
            canvas.DrawRect(bounds, warningPaint);
        }

        public void DrawPlayerTextInfo(SKCanvas canvas, Player player, float distance, Vector2 screenPos, AimviewObjectSettings objectSettings, PlayerTypeSettings typeSettings)
        {
            var textPaint = player.GetAimviewTextPaint();
            textPaint.TextSize = 14f * uiScale;
            textPaint.TextAlign = SKTextAlign.Left;
            var textX = screenPos.X + 20;
            var currentY = screenPos.Y + 20;

            // 名前の描画
            if (typeSettings.ShowName && objectSettings.Name && !string.IsNullOrEmpty(player.Name))
            {
                // プレイヤー名を描画
                canvas.DrawText(player.Name, textX, currentY, textPaint);
                
                // アンダーラインを描画（プレイヤー名の強調）
                float textWidth = textPaint.MeasureText(player.Name);
                using var underlinePaint = new SKPaint
                {
                    Color = textPaint.Color,
                    StrokeWidth = 1,
                    Style = SKPaintStyle.Stroke
                };
                canvas.DrawLine(textX, currentY + 2, textX + textWidth, currentY + 2, underlinePaint);
                
                currentY += textPaint.TextSize * uiScale;
            }

            // 武器情報の描画
            if (typeSettings.ShowWeapon && 
                player.ItemInHands.Item is not null && 
                !string.IsNullOrEmpty(player.ItemInHands.Item.Short))
            {
                var weaponText = player.ItemInHands.Item.Short;
                if (!string.IsNullOrEmpty(player.ItemInHands.Item.GearInfo.AmmoType))
                {
                    var ammoMsg = player.isOfflinePlayer ? $"/{player.ItemInHands.Item.GearInfo.AmmoCount}" : "";
                    weaponText += $" ({player.ItemInHands.Item.GearInfo.AmmoType}{ammoMsg})";
                }
                
                canvas.DrawText(weaponText, textX, currentY, textPaint);
                currentY += textPaint.TextSize * uiScale;
            }

            // ヘルス情報の描画
            if (typeSettings.ShowHealth && !string.IsNullOrEmpty(player.HealthStatus))
            {
                canvas.DrawText(player.HealthStatus, textX, currentY, textPaint);
                currentY += textPaint.TextSize * uiScale;
            }

            // 距離情報の描画
            if (typeSettings.ShowDistance && objectSettings.Distance)
            {
                canvas.DrawText($"{distance:F0}m", textX, currentY, textPaint);
            }
        }

        private readonly struct BoneConnection
        {
            public PlayerBones Start { get; init; }
            public PlayerBones End { get; init; }
        }

        private static readonly BoneConnection[] BoneConnections = new[]
        {
            // メインボディ
            new BoneConnection { Start = PlayerBones.HumanHead, End = PlayerBones.HumanSpine3 },
            new BoneConnection { Start = PlayerBones.HumanSpine3, End = PlayerBones.HumanPelvis },

            // 腕
            new BoneConnection { Start = PlayerBones.HumanSpine3, End = PlayerBones.HumanLForearm1 },
            new BoneConnection { Start = PlayerBones.HumanLForearm1, End = PlayerBones.HumanLPalm },
            new BoneConnection { Start = PlayerBones.HumanSpine3, End = PlayerBones.HumanRForearm1 },
            new BoneConnection { Start = PlayerBones.HumanRForearm1, End = PlayerBones.HumanRPalm },

            // 脚
            new BoneConnection { Start = PlayerBones.HumanPelvis, End = PlayerBones.HumanLCalf },
            new BoneConnection { Start = PlayerBones.HumanLCalf, End = PlayerBones.HumanLFoot },
            new BoneConnection { Start = PlayerBones.HumanPelvis, End = PlayerBones.HumanRCalf },
            new BoneConnection { Start = PlayerBones.HumanRCalf, End = PlayerBones.HumanRFoot }
        };

        private SKColor GetPlayerColor(Player player)
        {
            return player.GetAimviewPaint().Color;
        }

        private void DrawPlayerSkeleton(Player player, SKCanvas canvas)
        {
            if (player?.Bones == null)
                return;

            // プレイヤータイプごとの設定を取得
            var typeSettings = GetPlayerTypeSettings(player.Type);
            var objectSettings = config.AimviewSettings.ObjectSettings["Player"];
            
            // 距離チェック
            var distance = Vector3.Distance(player.Position, _espSystem.LocalPlayer?.Position ?? Vector3.Zero);
            var maxDistance = Math.Max(objectSettings.PaintDistance, objectSettings.TextDistance);
            if (distance > maxDistance)
                return;

            var bounds = canvas.DeviceClipBounds;
            var bonePositions = _espSystem.GetPlayerBonePositions(player.ProfileID);
            if (bonePositions == null || bonePositions.Count == 0)
                return;

            // ワールド座標をまとめて取得
            var worldPositions = new List<Vector3>();
            var boneTypes = new List<PlayerBones>();
            foreach (var kvp in bonePositions)
            {
                worldPositions.Add(kvp.Value);
                boneTypes.Add(kvp.Key);
            }

            // スクリーン座標に一括変換（SIMD使用）
            var screenPositions = new List<Vector2>();
            var results = Extensions.WorldToScreenCombinedSIMD(worldPositions, bounds.Width, bounds.Height, screenPositions);

            // 変換結果をディクショナリに格納
            var screenCoords = new Dictionary<PlayerBones, Vector2>();
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i])
                {
                    screenCoords[boneTypes[i]] = screenPositions[i];
                }
            }

            // ボーンの描画
            using var paint = new SKPaint
            {
                Color = GetPlayerColor(player),
                StrokeWidth = 1,
                IsAntialias = true
            };

            // 各ボーンラインを描画
            foreach (var boneLine in BoneConnections)
            {
                if (screenCoords.TryGetValue(boneLine.Start, out var start) && 
                    screenCoords.TryGetValue(boneLine.End, out var end))
                {
                    canvas.DrawLine(start.X, start.Y, end.X, end.Y, paint);
                }
            }

            // 頭部インジケーターを描画
            if (screenCoords.TryGetValue(PlayerBones.HumanHead, out var headPos))
            {
                var headRadius = CalculateDistanceBasedSize(distance);
                paint.Style = SKPaintStyle.Stroke;
                canvas.DrawCircle(headPos.X, headPos.Y, headRadius, paint);
            }
        }

        public Vector2 DrawPlayerBox(SKCanvas canvas, Player player, float distance, AimviewObjectSettings objectSettings)
        {
            if (player?.Bones == null || distance >= objectSettings.PaintDistance)
                return Vector2.Zero;

            // 頭と骨盤のボーンの位置を取得
            if (!player.Bones.TryGetValue(PlayerBones.HumanHead, out var headBone) ||
                !player.Bones.TryGetValue(PlayerBones.HumanPelvis, out var pelvisBone))
                return Vector2.Zero;

            // ボーンの位置を更新
            headBone.UpdatePosition();
            pelvisBone.UpdatePosition();

            var bounds = GetAimviewBounds(canvas);
            if (!TryGetScreenPosition(headBone.Position, bounds, out Vector2 headScreenPos) ||
                !TryGetScreenPosition(pelvisBone.Position, bounds, out Vector2 pelvisScreenPos))
                return Vector2.Zero;

            // ボックスのサイズを計算
            float height = pelvisScreenPos.Y - headScreenPos.Y;


            // 上に10%、下に30%高さを伸ばす
            float topExtension = height * 0.2f;    // 上部の伸び
            float bottomExtension = height * 1.3f;  // 下部の伸び
            float extendedHeight = height + topExtension + bottomExtension;

            float width = extendedHeight * 0.4f;

            using var paint = player.GetAimviewPaint();
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = Math.Max(1f, 2f * (1f - distance / objectSettings.PaintDistance));

            var boxRect = SKRect.Create(
                headScreenPos.X - width / 2,
                headScreenPos.Y - topExtension,
                width,
                extendedHeight
            );

            canvas.DrawRect(boxRect, paint);

            // 頭部インジケーターを描画
            var headRadius = CalculateDistanceBasedSize(distance);
            paint.Style = SKPaintStyle.Stroke;
            canvas.DrawCircle(headScreenPos.X, headScreenPos.Y, headRadius, paint);

            return pelvisScreenPos;
        }

        public void DrawPlayerDot(SKCanvas canvas, Player player, Vector2 screenPos, float distance, AimviewObjectSettings objectSettings)
        {
            if (distance >= objectSettings.PaintDistance)
                return;

            // プレイヤーの位置を頭の位置から取得
            if (player?.Bones == null || !player.Bones.TryGetValue(PlayerBones.HumanHead, out var headBone))
                return;

            // 頭の位置を更新
            headBone.UpdatePosition();
            var bounds = GetAimviewBounds(canvas);
            
            // 頭の位置をスクリーン座標に変換
            if (!TryGetScreenPosition(headBone.Position, bounds, out Vector2 headScreenPos))
                return;

            var paint = player.GetAimviewPaint();
            paint.Style = SKPaintStyle.Stroke;
            
            var dotSize = CalculateDistanceBasedSize(distance);
            canvas.DrawCircle(headScreenPos.X, headScreenPos.Y, dotSize, paint);
        }

        public float CalculateDistanceBasedSize(float distance, float maxSize = 8.0f, float minSize = 1.0f, float decayFactor = 0.02f)
        {
            var scale = (float)Math.Exp(-decayFactor * distance);
            return ((maxSize - minSize) * scale + minSize) * uiScale;
        }

        public bool IsWithinDrawingBounds(Vector2 pos, SKRect bounds)
        {
            return pos.X >= bounds.Left && pos.X <= bounds.Right &&
                   pos.Y >= bounds.Top && pos.Y <= bounds.Bottom;
        }

        public SKRect GetAimviewBounds(SKCanvas canvas)
        {
            return new SKRect(0, 0, canvas.DeviceClipBounds.Width, canvas.DeviceClipBounds.Height);
        }

        public bool TryGetScreenPosition(Vector3 worldPosition, SKRect bounds, out Vector2 screenPosition)
        {
            screenPosition = Vector2.Zero;

            try
            {
                var isVisible = Extensions.WorldToScreen(worldPosition, (int)bounds.Width, (int)bounds.Height, out screenPosition);

                if (isVisible)
                {
                    screenPosition.X += bounds.Left;
                    screenPosition.Y += bounds.Top;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Program.Log($"[TryGetScreenPosition] Error: {ex.Message}");
                return false;
            }
        }

        public PlayerTypeSettings GetPlayerTypeSettings(PlayerType type)
        {
            // BEARとUSECの場合はPMCの設定を使用
            if (type == PlayerType.BEAR || type == PlayerType.USEC)
            {
                type = PlayerType.PMC;
            }

            // 設定が存在しない場合は新しい設定を作成
            if (!this.config.AimviewSettings.PlayerTypeSettings.TryGetValue(type, out var settings))
            {
                settings = new PlayerTypeSettings
                {
                    ShowName = true,
                    ShowDistance = true,
                    ShowWeapon = true,
                    ShowHealth = true,
                    ShowTargetedAlert = true,
                    ESPStyle = ESPStyle.Skeleton
                };
                this.config.AimviewSettings.PlayerTypeSettings[type] = settings;
            }
            return settings;
        }

        public void DrawLootItems(SKCanvas canvas, IEnumerable<LootableObject> lootItems, Vector3 myPosition, AimviewObjectSettings settings)
        {
            foreach (var item in lootItems)
            {
                var dist = Vector3.Distance(myPosition, item.Position);
                if (dist > Math.Max(settings.PaintDistance, settings.TextDistance))
                    continue;

                var screenPos = WorldToScreen(item.Position, canvas);
                if (screenPos == null)
                    continue;

                DrawLootableObject(canvas, item, screenPos.Value, dist, settings);
            }
        }

        public void DrawTripwires(SKCanvas canvas, IEnumerable<Tripwire> tripwires, Vector3 myPosition, AimviewObjectSettings settings)
        {
            foreach (var tripwire in tripwires)
            {
                var dist = Vector3.Distance(myPosition, tripwire.FromPos);
                if (dist > Math.Max(settings.PaintDistance, settings.TextDistance))
                    continue;

                var fromScreenPos = WorldToScreen(tripwire.FromPos, canvas);
                var toScreenPos = WorldToScreen(tripwire.ToPos, canvas);
                if (fromScreenPos == null || toScreenPos == null)
                    continue;

                DrawTripwire(canvas, tripwire, fromScreenPos.Value, toScreenPos.Value, dist, settings);
            }
        }

        public void DrawExfils(SKCanvas canvas, IEnumerable<Exfil> exfils, Vector3 myPosition, AimviewObjectSettings settings)
        {
            foreach (var exfil in exfils)
            {
                var dist = Vector3.Distance(myPosition, exfil.Position);
                if (dist > settings.TextDistance)
                    continue;

                var screenPos = WorldToScreen(exfil.Position, canvas);
                if (screenPos == null)
                    continue;

                DrawExfil(canvas, exfil, screenPos.Value, dist, settings);
            }
        }

        public void DrawTransits(SKCanvas canvas, IEnumerable<Transit> transits, Vector3 myPosition, AimviewObjectSettings settings)
        {
            foreach (var transit in transits)
            {
                var dist = Vector3.Distance(myPosition, transit.Position);
                if (dist > settings.TextDistance)
                    continue;

                var screenPos = WorldToScreen(transit.Position, canvas);
                if (screenPos == null)
                    continue;

                DrawTransit(canvas, transit, screenPos.Value, dist, settings);
            }
        }

        private void DrawLootableObject(SKCanvas canvas, LootableObject lootObject, Vector2 screenPos, float distance, AimviewObjectSettings objectSettings)
        {
            var currentY = screenPos.Y;

            if (distance < objectSettings.PaintDistance)
            {
                var paint = lootObject.GetAimviewPaint();
                canvas.DrawCircle(screenPos.X, currentY, 3f * uiScale, paint);
                currentY += (AIMVIEW_OBJECT_SPACING + 3f) * uiScale;
            }

            if (distance < objectSettings.TextDistance)
            {
                var textPaint = lootObject.GetAimviewTextPaint();
                textPaint.TextSize = 12f * uiScale;
                textPaint.TextAlign = SKTextAlign.Center;

                if (objectSettings.Name)
                {
                    var name = lootObject switch
                    {
                        LootItem item => item.Item.shortName,
                        LootContainer container => container.Name,
                        LootCorpse corpse => corpse.Name,
                        _ => string.Empty
                    };
                    canvas.DrawText(name, screenPos.X, currentY + textPaint.TextSize, textPaint);
                    currentY += textPaint.TextSize * uiScale;
                }

                if (objectSettings.Value)
                {
                    var value = lootObject switch
                    {
                        LootItem item => TarkovDevManager.GetItemValue(item.Item),
                        LootContainer container => container.Value,
                        LootCorpse corpse => corpse.Value,
                        _ => 0
                    };
                    canvas.DrawText($"{value:N0}₽", screenPos.X, currentY + textPaint.TextSize, textPaint);
                    currentY += textPaint.TextSize * uiScale;
                }

                if (objectSettings.Distance)
                    canvas.DrawText($"{distance:F0}m", screenPos.X, currentY + textPaint.TextSize, textPaint);
            }
        }

        private void DrawTripwire(SKCanvas canvas, Tripwire tripwire, Vector2 fromScreenPos, Vector2 toScreenPos, float distance, AimviewObjectSettings objectSettings)
        {
            if (distance < objectSettings.PaintDistance)
            {
                var tripwirePaint = tripwire.GetAimviewPaint();
                canvas.DrawLine(fromScreenPos.X, fromScreenPos.Y, toScreenPos.X, toScreenPos.Y, tripwirePaint);
            }

            if (distance < objectSettings.TextDistance)
            {
                var textPaint = tripwire.GetAimviewTextPaint();
                textPaint.TextSize = 12f * uiScale;
                var currentY = fromScreenPos.Y;

                if (objectSettings.Name)
                {
                    canvas.DrawText("Tripwire", fromScreenPos.X, currentY, textPaint);
                    currentY += textPaint.TextSize * uiScale;
                }

                if (objectSettings.Distance)
                    canvas.DrawText($"{distance:F0}m", fromScreenPos.X, currentY, textPaint);
            }
        }

        private void DrawExfil(SKCanvas canvas, Exfil exfil, Vector2 screenPos, float distance, AimviewObjectSettings objectSettings)
        {
            var textPaint = exfil.GetAimviewTextPaint();
            textPaint.TextSize = 14f * uiScale;

            var currentY = screenPos.Y;

            if (objectSettings.Name)
            {
                canvas.DrawText(exfil.Name, screenPos.X, currentY, textPaint);
                currentY += textPaint.TextSize * uiScale;
            }

            if (objectSettings.Distance)
                canvas.DrawText($"{distance:F0}m", screenPos.X, currentY, textPaint);
        }

        private void DrawTransit(SKCanvas canvas, Transit transit, Vector2 screenPos, float distance, AimviewObjectSettings objectSettings)
        {
            var textPaint = transit.GetAimviewTextPaint();
            textPaint.TextSize = 14f * uiScale;

            var currentY = screenPos.Y;

            if (objectSettings.Name)
            {
                canvas.DrawText(transit.Name, screenPos.X, currentY, textPaint);
                currentY += textPaint.TextSize * uiScale;
            }

            if (objectSettings.Distance)
                canvas.DrawText($"{distance:F0}m", screenPos.X, currentY, textPaint);
        }

        public Vector2? WorldToScreen(Vector3 worldPosition, SKCanvas canvas)
        {
            try
            {
                Vector2 screenPos;
                var bounds = GetAimviewBounds(canvas);
                var isVisible = Extensions.WorldToScreen(worldPosition, (int)bounds.Width, (int)bounds.Height, out screenPos);

                if (isVisible)
                {
                    screenPos.X += bounds.Left;
                    screenPos.Y += bounds.Top;
                    return screenPos;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public void UpdatePlayers(Player localPlayer, ReadOnlyDictionary<string, Player> allPlayers)
        {
            _espSystem.UpdatePlayers(localPlayer, allPlayers);
        }

        public void Dispose()
        {
            _espSystem?.Dispose();
        }

        // アモ表示をクリアするメソッドを追加
        public void ClearAmmoDisplay()
        {
            // アモ表示に関連する状態をリセット
            _lastAmmoCount = 0;
            _lastMaxAmmo = 0;
            _lastAmmoType = null;
            _lastAmmoTypeLong = null;
        }

        private int _lastAmmoCount = 0;
        private int _lastMaxAmmo = 0;
        private string _lastAmmoType = null;
        private string _lastAmmoTypeLong = null;

        // 残弾数テキストを右寄せで描画
        private void DrawRightAlignedAmmoText(SKCanvas canvas, float endX, float y, string msg, SKColor color, float fontSize = 20, string fontFamily = "Arial Unicode MS", float boxWidth = 185)
        {
            // 右寄せのための開始X座標を計算
            float startX = endX - boxWidth;

            // テキストペイントの設定
            using var paint = new SKPaint
            {
                Color = color,
                TextSize = fontSize,
                TextAlign = SKTextAlign.Right,
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName(fontFamily)
            };

            // テキストを描画
            canvas.DrawText(msg, endX, y + fontSize, paint);
        }

        // ローカルプレイヤーの残弾数を描画
        public void DrawLocalPlayerAmmo(SKCanvas canvas, Player localPlayer)
        {
            if (localPlayer == null || 
                localPlayer.ItemInHands.Item.GearInfo.AmmoType == null)
            {
                // プレイヤーがnullの場合は最後の状態をクリア
                _lastAmmoCount = 0;
                _lastMaxAmmo = 0;
                _lastAmmoType = null;
                _lastAmmoTypeLong = null;
                return;
            }

            // 現在の状態を保存
            _lastAmmoCount = localPlayer.ItemInHands.Item.GearInfo.AmmoCount;
            _lastMaxAmmo = localPlayer.ItemInHands.Item.GearInfo.MaxMagCount;
            _lastAmmoType = localPlayer.ItemInHands.Item.GearInfo.AmmoType;
            _lastAmmoTypeLong = localPlayer.ItemInHands.Item.GearInfo.AmmoTypeLong;

            // 縦バーの位置（画面右端）
            float barX = canvas.LocalClipBounds.Width - 40;
            float barY = canvas.LocalClipBounds.Height - 270;

            // 残弾数バーを描画
            DrawAmmoBar(
                canvas,
                barX,
                barY,
                _lastAmmoCount,
                _lastMaxAmmo,
                12,
                250
            );

            // テキスト表示位置（バーの左側）
            float textEndX = barX - 20;

            // アモタイプを表示（上）
            DrawRightAlignedAmmoText(
                canvas,
                textEndX,
                barY + 10,
                _lastAmmoTypeLong ?? _lastAmmoType,
                SKColors.LightGray,
                16,
                "Bahnschrift",
                200
            );

            // 残弾数テキストを描画（下）
            string ammoText = $"{_lastAmmoCount} / {_lastMaxAmmo}";
            DrawRightAlignedAmmoText(
                canvas,
                textEndX,
                barY + 30,
                ammoText,
                SKColors.White,
                32,
                "DIN",
                200
            );
        }

        // 残弾数バーを描画（縦方向）
        private void DrawAmmoBar(SKCanvas canvas, float x, float y, int currentAmmo, int maxAmmo, float barWidth = 8, float barHeight = 100)
        {
            // ボーダーの太さ
            float borderThickness = 1.0f;

            // 残弾率を計算
            float ammoPercentage = (float)currentAmmo / maxAmmo;

            // バーの実際の描画領域を計算
            float innerWidth = barWidth - (borderThickness * 2);
            float innerHeight = barHeight - (borderThickness * 2);

            // 外枠を描画（グレー）
            using var borderPaint = new SKPaint
            {
                Color = new SKColor(128, 128, 128, 255),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = borderThickness
            };
            canvas.DrawRect(x, y, barWidth, barHeight, borderPaint);

            // 内側の背景を描画（暗めのグレー）
            using var bgPaint = new SKPaint
            {
                Color = new SKColor(45, 45, 45, 255),
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(
                x + borderThickness,
                y + borderThickness,
                innerWidth,
                innerHeight,
                bgPaint
            );

            // 残弾数に応じて色を変化（多い：緑 → 少ない：赤）
            float r = Math.Max(1.0f - ammoPercentage, 0.0f);
            float g = Math.Min(ammoPercentage, 1.0f);
            SKColor fillColor = new SKColor((byte)(r * 255), (byte)(g * 255), 0, 255);

            // フィルバーを描画（上から減少）
            using var fillPaint = new SKPaint
            {
                Color = fillColor,
                Style = SKPaintStyle.Fill
            };

            float filledHeight = innerHeight * ammoPercentage;
            canvas.DrawRect(
                x + borderThickness,
                y + borderThickness + (innerHeight - filledHeight), // 下端からの位置を計算
                innerWidth,
                filledHeight,
                fillPaint
            );
        }

        public void DrawQuestItem(SKCanvas canvas, QuestItem item, Vector2 screenPos, float distance, AimviewObjectSettings objectSettings)
        {
            var currentY = screenPos.Y;

            if (distance < objectSettings.PaintDistance)
            {
                using var paint = new SKPaint
                {
                    Color = Extensions.SKColorFromPaintColor("QuestItem"),
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };
                var objectSize = CalculateDistanceBasedSize(distance);
                canvas.DrawCircle(screenPos.X, currentY, objectSize, paint);
                currentY += (objectSize + AIMVIEW_OBJECT_SPACING) * uiScale;
            }

            if (distance < objectSettings.TextDistance)
            {
                using var textPaint = new SKPaint
                {
                    Color = Extensions.SKColorFromPaintColor("QuestItem"),
                    TextSize = 14f * uiScale,
                    TextAlign = SKTextAlign.Center,
                    IsAntialias = true
                };

                if (objectSettings.Name)
                {
                    canvas.DrawText(item.Name, screenPos.X, currentY, textPaint);
                    currentY += textPaint.TextSize * uiScale;
                }

                if (objectSettings.Distance)
                {
                    canvas.DrawText($"{distance:F0}m", screenPos.X, currentY, textPaint);
                }
            }
        }

        public void DrawQuestZone(SKCanvas canvas, QuestZone zone, Vector2 screenPos, float distance, AimviewObjectSettings objectSettings)
        {
            var currentY = screenPos.Y;

            if (distance < objectSettings.PaintDistance)
            {
                using var paint = new SKPaint
                {
                    Color = Extensions.SKColorFromPaintColor("QuestZone"),
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };
                var objectSize = CalculateDistanceBasedSize(distance);
                canvas.DrawCircle(screenPos.X, currentY, objectSize, paint);
                currentY += (objectSize + AIMVIEW_OBJECT_SPACING) * uiScale;
            }

            if (distance < objectSettings.TextDistance)
            {
                using var textPaint = new SKPaint
                {
                    Color = Extensions.SKColorFromPaintColor("QuestZone"),
                    TextSize = 14f * uiScale,
                    TextAlign = SKTextAlign.Center,
                    IsAntialias = true
                };

                if (objectSettings.Name)
                {
                    canvas.DrawText(zone.ObjectiveType, screenPos.X, currentY, textPaint);
                    currentY += textPaint.TextSize * uiScale;

                    if (!string.IsNullOrEmpty(zone.Description))
                    {
                        canvas.DrawText(zone.Description, screenPos.X, currentY, textPaint);
                        currentY += textPaint.TextSize * uiScale;
                    }
                }

                if (objectSettings.Distance)
                {
                    canvas.DrawText($"{distance:F0}m", screenPos.X, currentY, textPaint);
                }
            }
        }

        public void DrawQuestItems(SKCanvas canvas, IEnumerable<QuestItem> questItems, Vector3 myPosition, AimviewObjectSettings settings, bool includeUnknown)
        {
            var items = includeUnknown ?
                questItems.Where(x => x?.Position.X != 0 && x?.Name == "????") :
                questItems.Where(x => x?.Position.X != 0 && x?.Name != "????" && !x.Complete);

            foreach (var item in items)
            {
                var dist = Vector3.Distance(myPosition, item.Position);
                if (dist > Math.Max(settings.PaintDistance, settings.TextDistance))
                    continue;

                var screenPos = WorldToScreen(item.Position, canvas);
                if (screenPos == null || !IsWithinDrawingBounds(screenPos.Value, GetAimviewBounds(canvas)))
                    continue;

                DrawQuestItem(canvas, item, screenPos.Value, dist, settings);
            }
        }

        public void DrawQuestZones(SKCanvas canvas, IEnumerable<QuestZone> questZones, Vector3 myPosition, AimviewObjectSettings settings)
        {
            var mapName = Memory.MapNameFormatted.ToLower();
            var zones = questZones.Where(x => x.MapName.ToLower() == mapName && !x.Complete);

            foreach (var zone in zones)
            {
                var dist = Vector3.Distance(myPosition, zone.Position);
                if (dist > Math.Max(settings.PaintDistance, settings.TextDistance))
                    continue;

                var screenPos = WorldToScreen(zone.Position, canvas);
                if (screenPos == null || !IsWithinDrawingBounds(screenPos.Value, GetAimviewBounds(canvas)))
                    continue;

                DrawQuestZone(canvas, zone, screenPos.Value, dist, settings);
            }
        }
    }
} 