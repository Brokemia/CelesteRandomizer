﻿using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Linq;
using MonoMod.Cil;
using MonoMod.Utils;
using System.Collections.Generic;

namespace Celeste.Mod.Randomizer {
    public class RandoModule : EverestModule {
        public static RandoModule Instance;

        public RandoSettings Settings;
        public const int MAX_SEED_CHARS = 20;

        public RandoModule() {
            Instance = this;
            Settings = new RandoSettings();
        }

        public override void Load() {
            Everest.Events.MainMenu.OnCreateButtons += CreateMainMenuButton;
            Everest.Events.Level.OnCreatePauseMenuButtons += ModifyLevelMenu;
            Everest.Events.Level.OnTransitionTo += OnTransition;
            On.Celeste.OverworldLoader.ctor += EnterToRandoMenu;
            On.Celeste.Overworld.ctor += HideMaddy;
            On.Celeste.MapData.Load += DontLoadRandoMaps;
            On.Celeste.AreaData.Load += InitRandoData;
            On.Celeste.TextMenu.MoveSelection += DisableMenuMovement;
            On.Celeste.Cassette.CollectRoutine += NeverCollectCassettes;
            On.Celeste.AreaComplete.VersionNumberAndVariants += AreaCompleteDrawHash;
            On.Celeste.AngryOshiro.Added += DontSpawnTwoOshiros;
            On.Celeste.Player.Added += DontMoveOnWakeup;
            On.Celeste.BadelineOldsite.Added += PlayBadelineCutscene;
            On.Celeste.Textbox.ctor_string_Language_Func1Array += RandomizeTextboxText;
            On.Celeste.Level.LoadLevel += DontRestartTimer;
            IL.Celeste.Level.EnforceBounds += DisableUpTransition;
            IL.Celeste.Level.EnforceBounds += DontBlockOnTheo;
            IL.Celeste.TheoCrystal.Update += BeGracefulOnTransitions;
            //On.Celeste.AutoSplitterInfo.Update += wtf;
        }

        public override void LoadContent(bool firstLoad) {
        }


        public override void Unload() {
            Everest.Events.MainMenu.OnCreateButtons -= CreateMainMenuButton;
            Everest.Events.Level.OnCreatePauseMenuButtons -= ModifyLevelMenu;
            Everest.Events.Level.OnTransitionTo -= OnTransition;
            On.Celeste.OverworldLoader.ctor -= EnterToRandoMenu;
            On.Celeste.Overworld.ctor -= HideMaddy;
            On.Celeste.MapData.Load -= DontLoadRandoMaps;
            On.Celeste.AreaData.Load -= InitRandoData;
            On.Celeste.TextMenu.MoveSelection -= DisableMenuMovement;
            On.Celeste.Cassette.CollectRoutine -= NeverCollectCassettes;
            On.Celeste.AreaComplete.VersionNumberAndVariants += AreaCompleteDrawHash;
            On.Celeste.AngryOshiro.Added -= DontSpawnTwoOshiros;
            On.Celeste.Player.Added -= DontMoveOnWakeup;
            On.Celeste.BadelineOldsite.Added -= PlayBadelineCutscene;
            On.Celeste.Textbox.ctor_string_Language_Func1Array -= RandomizeTextboxText;
            On.Celeste.Level.LoadLevel -= DontRestartTimer;
            IL.Celeste.Level.EnforceBounds -= DisableUpTransition;
            IL.Celeste.Level.EnforceBounds -= DontBlockOnTheo;
            IL.Celeste.TheoCrystal.Update -= BeGracefulOnTransitions;
            //On.Celeste.AutoSplitterInfo.Update -= wtf;
        }

        public void DontRestartTimer(On.Celeste.Level.orig_LoadLevel orig, Level self, Player.IntroTypes playerIntro, bool fromLoader) {
            if (fromLoader && this.InRandomizer) {
                self.Session.FirstLevel = false;
            }
            orig(self, playerIntro, fromLoader);
        }

        public void wtf(On.Celeste.AutoSplitterInfo.orig_Update orig, AutoSplitterInfo self) {
            orig(self);
        }

        public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {
            base.CreateModMenuSection(menu, inGame, snapshot);
        }

        public bool InRandomizer {
            get {
                if (SaveData.Instance == null) {
                    return false;
                }
                if (SaveData.Instance.CurrentSession == null) {
                    return false;
                }
                if (SaveData.Instance.CurrentSession.Area == null) {
                    return false;
                }
                try {
                    AreaData area = AreaData.Get(SaveData.Instance.CurrentSession.Area);
                    return area.GetSID().StartsWith("randomizer/");
                } catch (NullReferenceException) {
                    return false; // I have no idea why this happens but it happens sometimes
                }
            }
        }

        public void CreateMainMenuButton(OuiMainMenu menu, System.Collections.Generic.List<MenuButton> buttons) {
            MainMenuSmallButton btn = new MainMenuSmallButton("MODOPTIONS_RANDOMIZER_TOPMENU", "menu/randomizer", menu, Vector2.Zero, Vector2.Zero, () => {
                Audio.Play(SFX.ui_main_button_select);
                Audio.Play(SFX.ui_main_whoosh_large_in);
                menu.Overworld.Goto<OuiRandoSettings>();
            });
            buttons.Insert(1, btn);
        }

        public void ModifyLevelMenu(Level level, TextMenu pausemenu, bool minimal) {
            if (this.InRandomizer) {
                foreach (var item in new System.Collections.Generic.List<TextMenu.Item>(pausemenu.GetItems())) {
                    if (item.GetType() == typeof(TextMenu.Button)) {
                        var btn = (TextMenu.Button)item;
                        if (btn.Label == Dialog.Clean("MENU_PAUSE_SAVEQUIT") || btn.Label == Dialog.Clean("MENU_PAUSE_RETURN")) {
                            pausemenu.Remove(item);
                        }
                        if (btn.Label == Dialog.Clean("MENU_PAUSE_RESTARTAREA")) {
                            btn.Label = Dialog.Clean("MENU_PAUSE_RESTARTRANDO");
                        }
                    }
                }

                int returnIdx = pausemenu.GetItems().Count;
                pausemenu.Add(new TextMenu.Button(Dialog.Clean("MENU_PAUSE_QUITRANDO")).Pressed(() => {
                    level.PauseMainMenuOpen = false;
                    pausemenu.RemoveSelf();

                    TextMenu menu = new TextMenu();
                    menu.AutoScroll = false;
                    menu.Position = new Vector2((float)Engine.Width / 2f, (float)((double)Engine.Height / 2.0 - 100.0));
                    menu.Add(new TextMenu.Header(Dialog.Clean("MENU_QUITRANDO_TITLE")));
                    menu.Add(new TextMenu.Button(Dialog.Clean("MENU_QUITRANDO_CONFIRM")).Pressed((Action)(() => {
                        Engine.TimeRate = 1f;
                        menu.Focused = false;
                        level.Session.InArea = false;
                        Audio.SetMusic((string)null, true, true);
                        Audio.BusStopAll("bus:/gameplay_sfx", true);
                        level.DoScreenWipe(false, (Action)(() => Engine.Scene = (Scene)new LevelExit(LevelExit.Mode.SaveAndQuit, level.Session, level.HiresSnow)), true);
                        foreach (LevelEndingHook component in level.Tracker.GetComponents<LevelEndingHook>()) {
                            if (component.OnEnd != null)
                                component.OnEnd();
                        }
                    })));
                    menu.Add(new TextMenu.Button(Dialog.Clean("MENU_QUITRANDO_CANCEL")).Pressed((Action)(() => menu.OnCancel())));
                    menu.OnPause = menu.OnESC = (Action)(() => {
                        menu.RemoveSelf();
                        level.Paused = false;
                        Engine.FreezeTimer = 0.15f;
                        Audio.Play("event:/ui/game/unpause");
                    });
                    menu.OnCancel = (Action)(() => {
                        Audio.Play("event:/ui/main/button_back");
                        menu.RemoveSelf();
                        level.Pause(returnIdx, minimal, false);
                    });
                    level.Add((Entity)menu);
                }));
            }
        }

        public void OnTransition(Level level, LevelData next, Vector2 direction) {
            if (this.InRandomizer) {
                level.CoreMode = Session.CoreModes.None;
                level.Session.CoreMode = Session.CoreModes.None;

                var toRemove = new System.Collections.Generic.List<string>();
                foreach (var flag in level.Session.Flags) {
                    if (flag.StartsWith("summit_checkpoint_")) {
                        toRemove.Add(flag);
                    }
                }
                foreach (var flag in toRemove) {
                    level.Session.Flags.Remove(flag);
                }
            }
        }

        public void EnterToRandoMenu(On.Celeste.OverworldLoader.orig_ctor orig, OverworldLoader self, Overworld.StartMode startMode, HiresSnow snow) {
            if ((startMode == Overworld.StartMode.MainMenu || startMode == Overworld.StartMode.AreaComplete) && this.InRandomizer) {
                startMode = (Overworld.StartMode)55;
            }
            orig(self, startMode, snow);
        }

        // This is a bit of a hack. is there a better way to control this?
        public void HideMaddy(On.Celeste.Overworld.orig_ctor orig, Overworld self, OverworldLoader loader) {
            orig(self, loader);
            if (this.InRandomizer) {
                self.Maddy.Hide();
            }
        }

        public void DontLoadRandoMaps(On.Celeste.MapData.orig_Load orig, MapData self) {
            if (self.Data?.GetSID()?.StartsWith("randomizer/") ?? false) {
                return;
            }
            orig(self);
        }

        public void InitRandoData(On.Celeste.AreaData.orig_Load orig) {
            orig();
            RandoLogic.ProcessAreas();
            Settings.SetNormalMaps();
        }

        public void DisableMenuMovement(On.Celeste.TextMenu.orig_MoveSelection orig, TextMenu self, int direction, bool wiggle = false) {
            if (self is DisablableTextMenu newself && newself.DisableMovement) {
                return;
            }
            orig(self, direction, wiggle);
        }

        public IEnumerator NeverCollectCassettes(On.Celeste.Cassette.orig_CollectRoutine orig, Cassette self, Player player) {
            var thing = orig(self, player);
            while (thing.MoveNext()) {  // why does it not let me use foreach?
                yield return thing.Current;
            }

            if (this.InRandomizer) {
                var level = self.Scene as Level;
                level.Session.Cassette = false;
            }
        }

        public void AreaCompleteDrawHash(On.Celeste.AreaComplete.orig_VersionNumberAndVariants orig, string version, float ease, float alpha) {
            if (this.InRandomizer) {
                SaveData.Instance.VariantMode = false;
            }
            orig(version, ease, alpha);

            if (this.InRandomizer) {
                var text = this.Settings.Seed;
                if (this.Settings.Rules != Ruleset.Custom) {
                    text += " " + this.Settings.Rules.ToString();
                }
                text += "\n#" + this.Settings.Hash.ToString();
                ActiveFont.DrawOutline(text, new Vector2(1820f + 300f * (1f - Ease.CubeOut(ease)), 925f), new Vector2(0.5f, 0f), Vector2.One * 0.5f, Color.White, 2f, Color.Black);
            }
        }

        public void DontSpawnTwoOshiros(On.Celeste.AngryOshiro.orig_Added orig, AngryOshiro self, Scene scene) {
            orig(self, scene);
            var level = scene as Level;
            if (!level.Session.GetFlag("oshiro_resort_roof") && level.Session.Level.StartsWith("Celeste/3-CelestialResort/A/roof00")) {
                self.RemoveSelf();
            }
        }

        public void DontMoveOnWakeup(On.Celeste.Player.orig_Added orig, Player self, Scene scene) {
            orig(self, scene);
            if (this.InRandomizer) {
                self.JustRespawned = true;
            }
        }

        void RandomizeTextboxText(On.Celeste.Textbox.orig_ctor_string_Language_Func1Array orig, Textbox self, string dialog, Language language, Func<IEnumerator>[] events) {
            if (InRandomizer && RandoLogic.RandomDialogMappings.ContainsKey(dialog.ToLower())) {
                DynData<Textbox> selfData = new DynData<Textbox>(self);
                FancyText.Text origText = FancyText.Parse(Dialog.Get(dialog, language), (int)selfData.Get<float>("maxLineWidth"), selfData.Get<int>("linesPerPage"), 0f, null, language);
                var origTriggers = new List<FancyText.Trigger>(origText.Nodes.OfType<FancyText.Trigger>());
                orig(self, RandoLogic.RandomDialogMappings[dialog.ToLower()], language, events);

                // Replace triggers from randomized text with triggers from original text
                int origIndex = 0;
                for (int i = 0; i < self.Nodes.Count; i++) {
                    if (self.Nodes[i] is FancyText.Trigger trigger) {
                        if (origIndex < origTriggers.Count) {
                            trigger.Index = origTriggers[origIndex].Index;
                            trigger.Label = origTriggers[origIndex].Label;
                            trigger.Silent = origTriggers[origIndex].Silent;
                            origIndex++;
                        } else {
                            // This effectively disables the trigger if we've run out of original triggers
                            trigger.Index = -1;
                        }
                    }
                }
                // Add the remaining original triggers on to the end
                if (origIndex < origTriggers.Count) {
                    self.Nodes.AddRange(origTriggers.GetRange(origIndex, origTriggers.Count - origIndex));
                }
            } else {
                orig(self, dialog, language, events);
            }
        }

        public void PlayBadelineCutscene(On.Celeste.BadelineOldsite.orig_Added orig, BadelineOldsite self, Scene scene) {
            orig(self, scene);
            var level = scene as Level;
            if (!level.Session.GetFlag("evil_maddy_intro") && level.Session.Level.StartsWith("Celeste/2-OldSite/A/3")) {
                foreach (var c in self.Components) {
                    if (c is Coroutine) {
                        self.Components.Remove(c);
                        break;
                    }
                }

                self.Hovering = false;
                self.Visible = true;
                self.Hair.Visible = false;
                self.Sprite.Play("pretendDead", false, false);
                if (level.Session.Area.Mode == AreaMode.Normal) {
                    level.Session.Audio.Music.Event = null;
                    level.Session.Audio.Apply(false);
                }
                scene.Add(new CS02_BadelineIntro(self));
            }
        }

        public void DisableUpTransition(ILContext il) {
            ILCursor cursor = new ILCursor(il);
            cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallvirt<MapData>("CanTransitionTo"));
            cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallvirt<MapData>("CanTransitionTo"));
            cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallvirt<MapData>("CanTransitionTo"));
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Level, bool>>((level) => {
                if (!this.InRandomizer) {
                    return true;
                }
                var currentRoom = level.Session.LevelData;
                var extra = new DynData<LevelData>(currentRoom);
                if (extra.Get<bool?>("DisableUpTransition") ?? false) {
                    return false;
                }
                return true;
            });
            cursor.Emit(Mono.Cecil.Cil.OpCodes.And);
        }

        public void DontBlockOnTheo(ILContext il) {
            ILCursor cursor = new ILCursor(il);
            cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallvirt<Monocle.Tracker>("GetEntity"));
            /*cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_1);
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Level, Player, TheoCrystal, TheoCrystal>>((level, player, theo) => {
                if (this.InRandomizer && theo != null && player.Holding == null) {
                    level.Remove(theo);
                    return null;
                }
                return theo;
            });*/
            cursor.EmitDelegate<Func<TheoCrystal, TheoCrystal>>((theo) => {
                return this.InRandomizer ? null : theo;
            });
        }

        public void BeGracefulOnTransitions(ILContext il) {
            ILCursor cursor = new ILCursor(il);
            while (cursor.TryGotoNext(MoveType.Before, instr => instr.MatchCallvirt<Level>("get_Bounds"))) {
                cursor.Remove();
                cursor.EmitDelegate<Func<Level, Rectangle>>((level) => {
                    if (level.Transitioning && this.InRandomizer) {
                        return level.Session.MapData.Bounds;
                    }
                    return level.Bounds;
                });
            }
        }
    }

    public class DisablableTextMenu : TextMenu {
        public bool DisableMovement;
    }
}
