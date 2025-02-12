﻿using System.DirectoryServices;

namespace eft_dma_radar
{
    public class Toolbox
    {
        private Thread autoRefreshThread;
        private CancellationTokenSource autoRefreshCancellationTokenSource;

        private const int MAX_ATTEMPTS = 4;
        private bool _initialisingMonoAddresses = false;

        private bool medInfoPanel = false;
        private bool extendedReach = false;
        private bool infiniteStamina = false;
        
        private bool thermalVision = false;
        private bool nightVision = false;
        private bool frostBite = false;

        private bool thirdperson = false;

        private bool timeScale = false;
        private float timeScaleFactor = -1f;

        private Dictionary<string, bool> Skills = new Dictionary<string, bool>
        {
            ["Endurance"] = false,
            ["Strength"] = false,
            ["Vitality"] = false,
            ["Health"] = false,
            ["Stress Resistance"] = false,
            ["Metabolism"] = false,
            ["Perception"] = false,
            ["Intellect"] = false,
            ["Attention"] = false,
            ["MagDrills"] = false,
            ["Immunity"] = false,
            ["Throwables"] = false,
            ["Covert Movement"] = false,
            ["Search"] = false,
            ["Surgery"] = false,
            ["Light Vests"] = false,
            ["Heavy Vests"] = false
        };

        private Config _config { get => Program.Config; }
        private CameraManager _cameraManager { get => Memory.CameraManager; }
        private PlayerManager _playerManager { get => Memory.PlayerManager; }
        private Chams _chams { get => Memory.Chams; }
        private World _world{ get => Memory.World; }

        private ulong HardSettings;
        private ulong TimeScale;

        private bool ToolboxMonoInitialized = false;
        private bool FoundEFTHardSettings = false;
        private bool FoundTOD_Sky = false;
        private bool FoundWeatherController = false;
        private bool ShouldInitializeToolboxMono => !this.ToolboxMonoInitialized && Memory.InGame && Memory.LocalPlayer is not null; 
        public bool InitialisingMonoAddresses { get => this._initialisingMonoAddresses; }

        public bool UpdateExtendedReachDistance { get; set; } = false;
        public bool UpdateThermalSettings{ get; set; } = false;

        private bool IsSafeToWriteMemory => Memory.InGame && Memory.LocalPlayer is not null;

        public Toolbox(ulong unityBase)
        {
            if (this._config.MasterSwitch)
            {
                Task.Run(() =>
                {
                    var attempts = 0;
                    while (attempts < MAX_ATTEMPTS)
                    {
                        if (!this.ShouldInitializeToolboxMono)
                            break;

                        this.InitiateMonoAddresses();
                        Thread.Sleep(1000);
                        attempts++;
                    }
                });

                //this.InitiateTimeScale(unityBase);
                this.StartToolbox();
            }
        }

        public void StartToolbox()
        {
            if (this.autoRefreshThread is not null && this.autoRefreshThread.IsAlive)
            {
                return;
            }

            this.autoRefreshCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = this.autoRefreshCancellationTokenSource.Token;

            this.autoRefreshThread = new Thread(() => this.ToolboxWorkerThread(cancellationToken))
            {
                Priority = ThreadPriority.BelowNormal,
                IsBackground = true
            };
            this.autoRefreshThread.Start();
        }

        public async void StopToolbox()
        {
            await Task.Run(() =>
            {
                if (this.autoRefreshCancellationTokenSource is not null)
                {
                    this.autoRefreshCancellationTokenSource.Cancel();
                    this.autoRefreshCancellationTokenSource.Dispose();
                    this.autoRefreshCancellationTokenSource = null;
                }

                if (this.autoRefreshThread is not null)
                {
                    this.autoRefreshThread.Join();
                    this.autoRefreshThread = null;
                }
            });
        }

        private void ToolboxWorkerThread(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && this.IsSafeToWriteMemory)
            {
                if (this._config.MasterSwitch)
                {
                    Task.Run(() =>
                    {
                        this.ToolboxWorker();
                    });
                    Thread.Sleep(250);
                }
            }

            if (this._config.Chams["RevertOnClose"])
                this._chams?.ChamsDisable();
            else if (!Memory.InGame || Memory.LocalPlayer is null)
                this._chams?.RemovePointers();

            Program.Log("[ToolBox] Refresh thread stopped.");
        }

        private void InitiateMonoAddresses()
        {
            if (this.ShouldInitializeToolboxMono)
            {
                var attempts = 0;
                this._initialisingMonoAddresses = true;

                //while (attempts < MAX_ATTEMPTS && !this.FoundTOD_Sky)
                //{
                //    try
                //    {
                //        if (this._world.InitializeTOD_Sky())
                //        {
                //            this.FoundTOD_Sky = true;
                //            break;
                //        }
                //    }
                //    catch (Exception ex)
                //    {
                //        attempts++;
                //        Program.Log("[ToolBox] Failed to get TOD_SKY, retrying in 500ms!");
                //        Thread.Sleep(500);

                //        if (attempts == MAX_ATTEMPTS)
                //        {
                //            Program.Log($"[Toolbox] Failed to get TOD_Sky {MAX_ATTEMPTS} times, skipping!");
                //            break;
                //        }
                //    }
                //}

                attempts = 0;

                while (attempts < MAX_ATTEMPTS && !this.FoundEFTHardSettings)
                {
                    try
                    {
                        this.HardSettings = MonoSharp.GetStaticFieldDataOfClass("Assembly-CSharp", "EFTHardSettings");
                        this.FoundEFTHardSettings = true;
                    }
                    catch (Exception ex)
                    {
                        attempts++;
                        Program.Log("[ToolBox] Failed to get EFTHardSettings, retrying in 500ms!");
                        Thread.Sleep(500);

                        if (attempts == MAX_ATTEMPTS)
                        {
                            Program.Log($"[Toolbox] Failed to get EFTHardSettings {MAX_ATTEMPTS} times, skipping!");
                            break;
                        }
                    }
                }

                attempts = 0;

                //while (attempts < MAX_ATTEMPTS && !this.FoundWeatherController)
                //{
                //    try
                //    {
                //        if (this._world.InitializeWeatherController())
                //        {
                //            this.FoundWeatherController = true;
                //            break;
                //        }
                //    }
                //    catch (Exception ex)
                //    {
                //        attempts++;
                //        Program.Log("[ToolBox] Failed to get EFT.Weather.WeatherController, retrying in 500ms!");
                //        Thread.Sleep(500);

                //        if (attempts == MAX_ATTEMPTS)
                //        {
                //            Program.Log($"[Toolbox] Failed to get EFT.Weather.WeatherController {MAX_ATTEMPTS} times, skipping!");
                //            break;
                //        }
                //    }
                //}

                if (this.FoundTOD_Sky || this.FoundEFTHardSettings || this.FoundWeatherController)
                    this.ToolboxMonoInitialized = true;

                this._initialisingMonoAddresses = false;
                Program.Log($"[ToolBox] - Attempt at initialising Mono Addresses complete");
            }
            else
            {
                Program.Log($"[ToolBox] - InitiateMonoAddresses failed");
            }
        }

        private void ToolboxWorker()
        {
            if (Memory.Exfils?.Count < 1)
                return;

            try
            {
                var entries = new List<IScatterWriteEntry>();

                if (this._playerManager is not null)
                {
                    this._playerManager.UpdateVariables();

                    // No Recoil / Sway
                    this._playerManager.SetRecoil(this._config.Recoil, this._config.RecoilXPercent, this._config.RecoilYPercent, ref entries);
                    this._playerManager.SetWeaponSway(this._config.WeaponSway, this._config.WeaponSwayPercent, ref entries);

                    // Instant ADS
                    this._playerManager.SetInstantADS(this._config.InstantADS, ref entries);

                    // Loot Through Walls
                    this._playerManager.SetLootThroughWalls(this._config.LootThroughWalls, ref entries);

                    // Juggernaut
                    if (this._config.Juggernaut)
                        this._playerManager.SetJuggernaut(ref entries);

                    // No Weapon Malfunctions
                    if (this._config.NoWeaponMalfunctions)
                        this._playerManager.SetNoWeaponMalfunctions(ref entries);

                    // Thirdperson
                    if (this._config.Thirdperson != this.thirdperson)
                    {
                        this.thirdperson = this._config.Thirdperson;
                        this._playerManager.SetThirdPerson(this.thirdperson, ref entries);
                    }

                    #region Skill Buffs
                    if (this._config.MaxSkills["Endurance"] != this.Skills["Endurance"])
                    {
                        this.Skills["Endurance"] = this._config.MaxSkills["Endurance"];
                        this._playerManager.SetMaxSkillByCategory("Endurance", !this.Skills["Endurance"], ref entries);
                    }

                    if (this._config.MaxSkills["Strength"] != this.Skills["Strength"])
                    {
                        this.Skills["Strength"] = this._config.MaxSkills["Strength"];
                        this._playerManager.SetMaxSkillByCategory("Strength", !this.Skills["Strength"], ref entries);
                    }

                    if (this._config.MaxSkills["Vitality"] != this.Skills["Vitality"])
                    {
                        this.Skills["Vitality"] = this._config.MaxSkills["Vitality"];
                        this._playerManager.SetMaxSkillByCategory("Vitality", !this.Skills["Vitality"], ref entries);
                    }

                    if (this._config.MaxSkills["Health"] != this.Skills["Health"])
                    {
                        this.Skills["Health"] = this._config.MaxSkills["Health"];
                        this._playerManager.SetMaxSkillByCategory("Health", !this.Skills["Health"], ref entries);
                    }

                    if (this._config.MaxSkills["Stress Resistance"] != this.Skills["Stress Resistance"])
                    {
                        this.Skills["Stress Resistance"] = this._config.MaxSkills["Stress Resistance"];
                        this._playerManager.SetMaxSkillByCategory("Stress Resistance", !this.Skills["Stress Resistance"], ref entries);
                    }

                    if (this._config.MaxSkills["Metabolism"] != this.Skills["Metabolism"])
                    {
                        this.Skills["Metabolism"] = this._config.MaxSkills["Metabolism"];
                        this._playerManager.SetMaxSkillByCategory("Metabolism", !this.Skills["Metabolism"], ref entries);
                    }

                    if (this._config.MaxSkills["Perception"] != this.Skills["Perception"])
                    {
                        this.Skills["Perception"] = this._config.MaxSkills["Perception"];
                        this._playerManager.SetMaxSkillByCategory("Perception", !this.Skills["Perception"], ref entries);
                    }

                    if (this._config.MaxSkills["Intellect"] != this.Skills["Intellect"])
                    {
                        this.Skills["Intellect"] = this._config.MaxSkills["Intellect"];
                        this._playerManager.SetMaxSkillByCategory("Intellect", !this.Skills["Intellect"], ref entries);
                    }

                    if (this._config.MaxSkills["Attention"] != this.Skills["Attention"])
                    {
                        this.Skills["Attention"] = this._config.MaxSkills["Attention"];
                        this._playerManager.SetMaxSkillByCategory("Attention", !this.Skills["Attention"], ref entries);
                    }

                    if (this._config.MaxSkills["Mag Drills"] != this.Skills["MagDrills"])
                    {
                        this.Skills["MagDrills"] = this._config.MaxSkills["Mag Drills"];
                        this._playerManager.SetMaxSkillByCategory("MagDrills", !this.Skills["MagDrills"], ref entries);
                    }

                    if (this._config.MaxSkills["Immunity"] != this.Skills["Immunity"])
                    {
                        this.Skills["Immunity"] = this._config.MaxSkills["Immunity"];
                        this._playerManager.SetMaxSkillByCategory("Immunity", !this.Skills["Immunity"], ref entries);
                    }

                    if (this._config.MaxSkills["Throwables"] != this.Skills["Throwables"])
                    {
                        this.Skills["Throwables"] = this._config.MaxSkills["Throwables"];
                        this._playerManager.SetMaxSkillByCategory("Throwables", !this.Skills["Throwables"], ref entries);
                    }

                    if (this._config.MaxSkills["Covert Movement"] != this.Skills["Covert Movement"])
                    {
                        this.Skills["Covert Movement"] = this._config.MaxSkills["Covert Movement"];
                        this._playerManager.SetMaxSkillByCategory("Covert Movement", !this.Skills["Covert Movement"], ref entries);
                    }

                    if (this._config.MaxSkills["Search"] != this.Skills["Search"])
                    {
                        this.Skills["Search"] = this._config.MaxSkills["Search"];
                        this._playerManager.SetMaxSkillByCategory("Search", !this.Skills["Search"], ref entries);
                    }

                    if (this._config.MaxSkills["Surgery"] != this.Skills["Surgery"])
                    {
                        this.Skills["Surgery"] = this._config.MaxSkills["Surgery"];
                        this._playerManager.SetMaxSkillByCategory("Surgery", !this.Skills["Surgery"], ref entries);
                    }

                    if (this._config.MaxSkills["Light Vests"] != this.Skills["Light Vests"])
                    {
                        this.Skills["Light Vests"] = this._config.MaxSkills["Light Vests"];
                        this._playerManager.SetMaxSkillByCategory("Light Vests", !this.Skills["Light Vests"], ref entries);
                    }

                    if (this._config.MaxSkills["Heavy Vests"] != this.Skills["Heavy Vests"])
                    {
                        this.Skills["Heavy Vests"] = this._config.MaxSkills["Heavy Vests"];
                        this._playerManager.SetMaxSkillByCategory("Heavy Vests", !this.Skills["Heavy Vests"], ref entries);
                    }
                    #endregion

                    if (this._config.InfiniteStamina != this.infiniteStamina)
                    {
                        this.infiniteStamina = this._config.InfiniteStamina;
                        this._playerManager.SetInfiniteStamina(this._config.InfiniteStamina, ref entries);
                    }
                }

                // Mono stuff
                if (this.ToolboxMonoInitialized)
                {
                    // Extended Reach
                    if (this.FoundEFTHardSettings)
                    {
                        if (this._config.ExtendedReach != this.extendedReach || this.UpdateExtendedReachDistance)
                        {

                            if (this.UpdateExtendedReachDistance)
                                this.UpdateExtendedReachDistance = !this.UpdateExtendedReachDistance;
                            else
                                this.extendedReach = this._config.ExtendedReach;

                            this.SetInteractDistance(this.extendedReach, ref entries);
                        }

                        if (this._config.MedInfoPanel != this.medInfoPanel)
                        {
                            this.medInfoPanel = this._config.MedInfoPanel;
                            this.SetMedInfoPanel(this.medInfoPanel, ref entries);
                        }
                    }

                    // Lock time of day + set time of day
                    if (this.FoundTOD_Sky || this.FoundWeatherController)
                    {
                        var worldSettings = this._config.WorldSettings;

                        if (this.FoundTOD_Sky)
                        {
                            this._world.FreezeTime(worldSettings.FreezeTime, ref entries);
                            this._world.SetTimeOfDay(worldSettings.TimeOfDay, ref entries);
                            this._world.ModifySunSize(worldSettings.Sun, ref entries);
                            this._world.ModifyMoonSize(worldSettings.Moon, ref entries);
                            this._world.ModifyShadows(worldSettings.Shadows, ref entries);

                            this._world.ModifyDayLightIntensity(worldSettings.SunLight, worldSettings.SunLightIntensity, ref entries);
                            this._world.ModifyNightLightIntensity(worldSettings.MoonLight, worldSettings.MoonLightIntensity, ref entries);
                        }

                        if (this.FoundWeatherController)
                        {
                            this._world.ModifyCloudDensity(worldSettings.Clouds, ref entries);
                            this._world.ModifyFog(worldSettings.Fog, ref entries);
                            this._world.ModifyRain(worldSettings.Rain, ref entries);
                        }
                    }
                }

                // Camera Stuff
                if (this._cameraManager is not null)
                {
                    if (!this._cameraManager.IsReady)
                    {
                        this._cameraManager.UpdateCamera();
                    }
                    else
                    {
                        // No Visor
                        this._cameraManager.VisorEffect(this._config.NoVisor, ref entries);

                        // Inventory Blur
                        this._cameraManager.InventoryBlur(this._config.InventoryBlur, ref entries);

                        // Smart Thermal Vision
                        if (this._playerManager is not null && !this._playerManager.IsADS)
                        {
                            if (this._config.ThermalVision != this.thermalVision || this.UpdateThermalSettings)
                            {
                                this.thermalVision = this._config.ThermalVision;
                                this._cameraManager.ThermalVision(this.thermalVision, ref entries);
                                this.UpdateThermalSettings = false;
                            }
                        }
                        else
                        {
                            if (this._config.OpticThermalVision)
                            {
                                if (this.thermalVision)
                                {
                                    this.thermalVision = false;
                                    this._cameraManager.ThermalVision(false, ref entries);
                                }

                                this._cameraManager.OpticThermalVision(true, ref entries);
                            }
                            else
                            {
                                this._cameraManager.OpticThermalVision(false, ref entries);
                            }
                        }

                        if (this._config.FrostBite != this.frostBite)
                        {
                            this.frostBite = this._config.FrostBite;
                            this._cameraManager.FrostBite(this.frostBite, ref entries);
                        }

                        // Night Vision
                        if (this._config.NightVision != this.nightVision)
                        {
                            this.nightVision = this._config.NightVision;
                            this._cameraManager.NightVision(this.nightVision, ref entries);
                        }

                        // FOV - don't use, ghetto asf
                        //if (!this._playerManager.IsADS)
                            //this._cameraManager.SetFOV(this._config.FOV, ref entries);

                        // Chams
                        if (this._config.Chams["Enabled"])
                        {
                            this._chams.ChamsEnable();
                        }
                        else if (!this._config.Chams["Enabled"] && this._chams?.PlayersWithChamsCount > 0)
                        {
                            this._chams?.ChamsDisable();
                        }
                    }
                }

                // Time Scale
                //var timeScaleChanged = (this._config.TimeScale != this.timeScale);
                //var factorChanged = this._config.TimeScaleFactor != this.timeScaleFactor;

                //if (timeScaleChanged || (this._config.TimeScale && factorChanged))
                //{
                //    this.timeScale = this._config.TimeScale;

                //    var factor = this.timeScale ? this._config.TimeScaleFactor : 1f;

                //    if (factor != this.timeScaleFactor)
                //        this.SetTimeScaleFactor(factor, ref entries);
                //}

                if (entries.Any())
                    Memory.WriteScatter(entries);
            }
            catch (Exception ex)
            {
                Program.Log($"[ToolBox] ToolboxWorker ({ex.Message})\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Sets the maximum loot/door interaction distance
        /// </summary>
        /// <param name="enabled"></param>
        private void SetInteractDistance(bool on, ref List<IScatterWriteEntry> entries)
        {
            var pveMode = Memory.IsOfflinePvE;
            var maxDistance = (pveMode ? _config.ExtendedReachDistancePvE : _config.ExtendedReachDistance);
            var currentLootRaycastDistance = Memory.ReadValue<float>(this.HardSettings + Offsets.EFTHardSettings.LOOT_RAYCAST_DISTANCE);

            if (on && currentLootRaycastDistance != maxDistance)
            {
                entries.Add(new ScatterWriteDataEntry<float>(this.HardSettings + Offsets.EFTHardSettings.LOOT_RAYCAST_DISTANCE, maxDistance));
                entries.Add(new ScatterWriteDataEntry<float>(this.HardSettings + Offsets.EFTHardSettings.DOOR_RAYCAST_DISTANCE, maxDistance));
            }
            else if (!on && currentLootRaycastDistance == maxDistance)
            {
                entries.Add(new ScatterWriteDataEntry<float>(this.HardSettings + Offsets.EFTHardSettings.LOOT_RAYCAST_DISTANCE, 1.3f));
                entries.Add(new ScatterWriteDataEntry<float>(this.HardSettings + Offsets.EFTHardSettings.DOOR_RAYCAST_DISTANCE, 1f));
            }
        }

        private void SetMedInfoPanel(bool on, ref List<IScatterWriteEntry> entries)
        {
            entries.Add(new ScatterWriteDataEntry<bool>(this.HardSettings + Offsets.EFTHardSettings.MED_EFFECT_USING_PANEL, on));
        }

        private void InitiateTimeScale(ulong unityBase)
        {
            return;

            this.TimeScale = Memory.ReadValue<ulong>(unityBase + Offsets.ModuleBase.TimeScale + 7 * 8);
        }

        private void SetTimeScaleFactor(float factor, ref List<IScatterWriteEntry> entries)
        {
            return;

            entries.Add(new ScatterWriteDataEntry<float>(this.TimeScale + Offsets.TimeScale.Value, factor));
        }
    }
}
