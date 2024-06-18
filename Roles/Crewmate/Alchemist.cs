using AmongUs.GameOptions;
using Hazel;
using System.Text;
using TOHE.Modules;
using TOHE.Roles.Core;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate;

internal class Alchemist : RoleBase
{
    //===========================SETUP================================\\
    private const int Id = 6400;
    private static readonly HashSet<byte> playerIdList = [];
    public static bool HasEnabled => playerIdList.Any();
    
    public override CustomRoles ThisRoleBase => CustomRoles.Engineer;
    public override Custom_RoleType ThisRoleType => Custom_RoleType.CrewmateBasic;
    //==================================================================\\

    private static OptionItem VentCooldown;
    private static OptionItem ShieldDuration;
    private static OptionItem Vision;
    private static OptionItem VisionOnLightsOut;
    private static OptionItem VisionDuration;
    private static OptionItem Speed;
    private static OptionItem InvisDuration;

    private static Dictionary<byte, long> InvisTime = [];
    private static readonly Dictionary<byte, int> ventedId = [];
    public static readonly Dictionary<byte, byte> BloodlustList = [];

    private static byte PotionID = 10;
    private static string PlayerName = string.Empty;
    private static bool VisionPotionActive = false;
    private static bool FixNextSabo = false;
    private static bool IsProtected = false;

    public override void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Alchemist, 1);
        VentCooldown = FloatOptionItem.Create(Id + 11, "VentCooldown", new(0f, 70f, 1f), 15f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
            .SetValueFormat(OptionFormat.Seconds);
        ShieldDuration = FloatOptionItem.Create(Id + 12, "AlchemistShieldDur", new(5f, 70f, 1f), 20f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
            .SetValueFormat(OptionFormat.Seconds);
        Vision = FloatOptionItem.Create(Id + 16, "AlchemistVision", new(0f, 1f, 0.05f), 0.85f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
            .SetValueFormat(OptionFormat.Multiplier);
        VisionOnLightsOut = FloatOptionItem.Create(Id + 17, "AlchemistVisionOnLightsOut", new(0f, 1f, 0.05f), 0.4f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
            .SetValueFormat(OptionFormat.Multiplier);
        VisionDuration = FloatOptionItem.Create(Id + 18, "AlchemistVisionDur", new(5f, 70f, 1f), 20f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
            .SetValueFormat(OptionFormat.Seconds);
        Speed = FloatOptionItem.Create(Id + 19, "AlchemistSpeed", new(0.1f, 5f, 0.1f), 1.5f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
             .SetValueFormat(OptionFormat.Multiplier);
        InvisDuration = FloatOptionItem.Create(Id + 20, "AlchemistInvisDur", new(5f, 70f, 1f), 20f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
            .SetValueFormat(OptionFormat.Seconds);
        OverrideTasksData.Create(Id + 21, TabGroup.CrewmateRoles, CustomRoles.Alchemist);
    }

    public override void Init()
    {
        playerIdList.Clear();
        BloodlustList.Clear();
        PotionID = 10;
        PlayerName = string.Empty;
        ventedId.Clear();
        InvisTime.Clear();
        FixNextSabo = false;
        VisionPotionActive = false;
    }
    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        PlayerName = Utils.GetPlayerById(playerId).GetRealName();

        if (AmongUsClient.Instance.AmHost)
        {
            CustomRoleManager.OnFixedUpdateLowLoadOthers.Add(OnFixedUpdateInvis);
            AddBloodlus();
        }
    }
    public static void AddBloodlus()
    {
        if (AmongUsClient.Instance.AmHost)
        {
            CustomRoleManager.OnFixedUpdateLowLoadOthers.Add(OnFixedUpdatesBloodlus);
        }
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 1;

        if (VisionPotionActive)
        {
            opt.SetVisionV2();
            if (Utils.IsActive(SystemTypes.Electrical)) opt.SetFloat(FloatOptionNames.CrewLightMod, VisionOnLightsOut.GetFloat() * 5);
            else opt.SetFloat(FloatOptionNames.CrewLightMod, Vision.GetFloat());
        }
    }

    public override bool OnTaskComplete(PlayerControl player, int completedTaskCount, int totalTaskCount)
    {
        if (!player.IsAlive()) return true;

        var rand = IRandom.Instance;
        PotionID = (byte)rand.Next(1, 9);

        switch (PotionID)
        {
            case 1: // Shield
                player.Notify(GetString("AlchemistGotShieldPotion"), 15f);
                break;
            case 2: // Suicide
                player.Notify(GetString("AlchemistGotSuicidePotion"), 15f);
                break;
            case 3: // TP to random player
                player.Notify(GetString("AlchemistGotTPPotion"), 15f);
                break;
            case 4: // Speed
                player.Notify(GetString("AlchemistGotSpeedPotion"), 15f);
                break;
            case 5: // Quick fix next sabo
                FixNextSabo = true;
                PotionID = 10;
                player.Notify(GetString("AlchemistGotQFPotion"), 15f);
                break;
            case 6: // Bloodlust
                player.Notify(GetString("AlchemistGotBloodlustPotion"), 15f);
                break;
            case 7: // Increased vision
                player.Notify(GetString("AlchemistGotSightPotion"), 15f);
                break;
            case 8:
                player.Notify(GetString("AlchemistGotInvisibility"), 15f);
                break;
            default: // just in case
                break;
        }

        return true;
    }

    private static void SendRPC(PlayerControl pc)
    {
        if (pc.AmOwner) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetAlchemistTimer, SendOption.Reliable, pc.GetClientId());
        writer.Write((InvisTime.TryGetValue(pc.PlayerId, out var x) ? x : -1).ToString());
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        InvisTime.Clear();
        long invis = long.Parse(reader.ReadString());
        if (invis > 0) InvisTime.Add(PlayerControl.LocalPlayer.PlayerId, invis);
    }
    private static bool IsInvis(byte playerId) => InvisTime.ContainsKey(playerId);
    private static bool IsBloodlust(byte playerId) => BloodlustList.ContainsKey(playerId);

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        if (!IsProtected) return true;

        killer.SetKillCooldown(time: 5f);
        return false;
    }

    private static void OnFixedUpdatesBloodlus(PlayerControl player)
    {
        if (!IsBloodlust(player.PlayerId)) return;

        if (!player.IsAlive() || Pelican.IsEaten(player.PlayerId))
        {
            BloodlustList.Remove(player.PlayerId);
        }
        else
        {
            Vector2 bloodlustPos = player.transform.position;
            Dictionary<byte, float> targetDistance = [];
            float dis;
            foreach (var target in Main.AllAlivePlayerControls)
            {
                if (target.PlayerId != player.PlayerId && !target.Is(CustomRoles.Pestilence))
                {
                    dis = Vector2.Distance(bloodlustPos, target.transform.position);
                    targetDistance.Add(target.PlayerId, dis);
                }
            }
            if (targetDistance.Any())
            {
                var min = targetDistance.OrderBy(c => c.Value).FirstOrDefault();
                PlayerControl target = Utils.GetPlayerById(min.Key);
                var KillRange = NormalGameOptionsV07.KillDistances[Mathf.Clamp(Main.NormalOptions.KillDistance, 0, 2)];
                if (min.Value <= KillRange && player.CanMove && target.CanMove)
                {
                    if (player.RpcCheckAndMurder(target, true))
                    {
                        var bloodlustId = BloodlustList[player.PlayerId];
                        RPC.PlaySoundRPC(bloodlustId, Sounds.KillSound);
                        player.RpcMurderPlayer(target);
                        target.SetRealKiller(Utils.GetPlayerById(bloodlustId));
                        player.MarkDirtySettings();
                        target.MarkDirtySettings();
                        BloodlustList.Remove(player.PlayerId);
                        Utils.NotifyRoles(SpecifySeer: Utils.GetPlayerById(bloodlustId), SpecifyTarget: player, ForceLoop: true);
                    }
                }
            }
        }
    }
    private static long lastFixedTime;
    private void OnFixedUpdateInvis(PlayerControl player)
    {
        if (!IsInvis(player.PlayerId)) return;

        var now = Utils.GetTimeStamp();

        if (lastFixedTime != now)
        {
            lastFixedTime = now;
            Dictionary<byte, long> newList = [];
            List<byte> refreshList = [];
            foreach (var it in InvisTime)
            {
                var pc = Utils.GetPlayerById(it.Key);
                if (pc == null) continue;
                var remainTime = it.Value + (long)InvisDuration.GetFloat() - now;
                if (remainTime < 0)
                {
                    pc?.MyPhysics?.RpcBootFromVent(ventedId.TryGetValue(pc.PlayerId, out var id) ? id : Main.LastEnteredVent[pc.PlayerId].Id);
                    ventedId.Remove(pc.PlayerId);
                    pc.Notify(GetString("ChameleonInvisStateOut"));
                    pc.RpcResetAbilityCooldown();
                    SendRPC(pc);
                    continue;
                }
                else if (remainTime <= 10)
                {
                    if (!pc.IsModClient()) pc.Notify(string.Format(GetString("ChameleonInvisStateCountdown"), remainTime + 1));
                }
                newList.Add(it.Key, it.Value);
            }
            InvisTime.Where(x => !newList.ContainsKey(x.Key)).Do(x => refreshList.Add(x.Key));
            InvisTime = newList;
            refreshList.Do(x => SendRPC(Utils.GetPlayerById(x)));
        }
    }
    public static void OnReportDeadBodyGlobal()
    {
        lastFixedTime = new();
        BloodlustList.Clear();

        if (InvisTime.Any())
        {
            foreach (var alchemistId in playerIdList.ToArray())
            {
                if (!ventedId.ContainsKey(alchemistId)) continue;
                var alchemist = Utils.GetPlayerById(alchemistId);
                if (alchemist == null) return;

                alchemist?.MyPhysics?.RpcBootFromVent(ventedId.TryGetValue(alchemistId, out var id) ? id : Main.LastEnteredVent[alchemistId].Id);
                SendRPC(alchemist);
            }
        }

        InvisTime.Clear();
        ventedId.Clear();
    }

    public override void OnEnterVent(PlayerControl player, Vent vent)
    {
        NameNotifyManager.Notice.Remove(player.PlayerId);

        switch (PotionID)
        {
            case 1: // Shield
                IsProtected = true;
                player.Notify(GetString("AlchemistShielded"), ShieldDuration.GetInt());

                _ = new LateTask(() =>
                {
                    IsProtected = false;
                    player.Notify(GetString("AlchemistShieldOut"));

                }, ShieldDuration.GetInt(), "Alchemist Shield Is Out");
                break;
            case 2: // Suicide
                player.MyPhysics.RpcBootFromVent(vent.Id);
                _ = new LateTask(() =>
                {
                    Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.Poison;
                    player.SetRealKiller(player);
                    player.RpcMurderPlayer(player);

                }, 1f, "Alchemist Is Poisoned");
                break;
            case 3: // TP to random player
                _ = new LateTask(() =>
                {
                    List<PlayerControl> AllAlivePlayer = [.. Main.AllAlivePlayerControls.Where(x => x.CanBeTeleported() && x.PlayerId != player.PlayerId).ToArray()];
                    var target = AllAlivePlayer.RandomElement();
                    player.RpcTeleport(target.GetCustomPosition());
                    player.RPCPlayCustomSound("Teleport");
                }, 2f, "Alchemist teleported to random player");
                break;
            case 4: // Increased speed.;
                int SpeedDuration = 10;
                player.Notify(GetString("AlchemistHasSpeed"));
                var tempSpeed = Main.AllPlayerSpeed[player.PlayerId];
                Main.AllPlayerSpeed[player.PlayerId] = Speed.GetFloat();
                player.MarkDirtySettings();
                _ = new LateTask(() =>
                {
                    Main.AllPlayerSpeed[player.PlayerId] = Main.AllPlayerSpeed[player.PlayerId] - Speed.GetFloat() + tempSpeed;
                    player.Notify(GetString("AlchemistSpeedOut"));
                    player.MarkDirtySettings();
                }, SpeedDuration, "Alchemist: Set Speed to default");
                break;
            case 5: // Quick fix next sabo
                // Done when making the potion
                break;
            case 6: // Bloodlust
                player.Notify(GetString("AlchemistPotionBloodlust"));
                if (!IsBloodlust(player.PlayerId))
                {
                    BloodlustList.TryAdd(player.PlayerId, player.PlayerId);
                }
                break;
            case 7: // Increased vision
                VisionPotionActive = true;
                player.MarkDirtySettings();
                player.Notify(GetString("AlchemistHasVision"), VisionDuration.GetFloat());
                _ = new LateTask(() =>
                {
                    VisionPotionActive = false;
                    player.MarkDirtySettings();
                    player.Notify(GetString("AlchemistVisionOut"));

                }, VisionDuration.GetFloat(), "Alchemist Vision Is Out");
                break;
            case 8:
                // Invisibility
                // Handled in CoEnterVent
                break;
            case 10:
            default: // just in case
                player.Notify("NoPotion");
                break;
        }

        PotionID = 10;
    }

    public override void OnCoEnterVent(PlayerPhysics __instance, int ventId)
    {
        if (PotionID != 8) return;

        PotionID = 10;
        var pc = __instance.myPlayer;
        NameNotifyManager.Notice.Remove(pc.PlayerId);

        _ = new LateTask(() =>
        {
            ventedId.Remove(pc.PlayerId);
            ventedId.Add(pc.PlayerId, ventId);

            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.BootFromVent, SendOption.Reliable, pc.GetClientId());
            writer.WritePacked(ventId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);

            InvisTime.Add(pc.PlayerId, Utils.GetTimeStamp());
            SendRPC(pc);
            pc.Notify(GetString("ChameleonInvisState"), InvisDuration.GetFloat());

        }, 0.5f, "Alchemist Invis");
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        if (seer == null || isForMeeting || !isForHud || !seer.IsAlive()) return string.Empty;
        
        var str = new StringBuilder();
        if (IsInvis(seer.PlayerId))
        {
            var remainTime = InvisTime[seer.PlayerId] + (long)InvisDuration.GetFloat() - Utils.GetTimeStamp();
            str.Append(string.Format(GetString("ChameleonInvisStateCountdown"), remainTime + 1));
        }
        else 
        {
            switch (PotionID)
            {
                case 1: // Shield
                    str.Append(GetString("PotionStore") + GetString("StoreShield"));
                    break;
                case 2: // Suicide
                    str.Append(GetString("PotionStore") + GetString("StoreSuicide"));
                    break;
                case 3: // TP to random player
                    str.Append(GetString("PotionStore") + GetString("StoreTP"));
                    break;
                case 4: // Increased Speed
                    str.Append(GetString("PotionStore") + GetString("StoreSP"));
                    break;
                case 5: // Quick fix next sabo
                    str.Append(GetString("PotionStore") + GetString("StoreQF"));
                    break;
                case 6: // Bloodlust
                    str.Append(GetString("PotionStore") + GetString("StoreBL"));
                    break;
                case 7: // Increased vision
                    str.Append(GetString("PotionStore") + GetString("StoreNS"));
                    break;
                case 8: // Invisibility
                    str.Append(GetString("PotionStore") + GetString("StoreINV"));
                    break;
                case 10:
                    str.Append(GetString("PotionStore") + GetString("StoreNull"));
                    break;
                default: // just in case
                    break;
            }
            if (FixNextSabo) str.Append(GetString("WaitQFPotion"));
        }
        return str.ToString();
    }
    public override string GetProgressText(byte playerId, bool comms)
    {
        var player = Utils.GetPlayerById(playerId);
        if (player == null || !GameStates.IsInTask) return string.Empty;
        
        var str = new StringBuilder();
        switch (PotionID)
        {
            case 1: // Shield
                str.Append("<color=#00ff97>✚</color>");
                break;
            case 2: // Suicide
                str.Append("<color=#478800>⁂</color>");
                break;
            case 3: // TP to random player
                str.Append("<color=#42d1ff>§</color>");
                break;
            case 4: // Increased Speed
                str.Append("<color=#77e0cb>»</color>");
                break;
            case 5: // Quick fix next sabo
                str.Append("<color=#3333ff>★</color>");
                break;
            case 6: // Bloodlust
                str.Append("<color=#691a2e>乂</color>");
                break;
            case 7: // Increased vision
                str.Append("<color=#663399>◉</color>");
                break;
            case 8: //Invisibility
                str.Append("<color=#b3b3b3>◌</color>");
                break;
            default:
                break;
        }
        if (FixNextSabo) str.Append("<color=#3333ff>★</color>");
        
        return str.ToString();
    }
    public override void UpdateSystem(ShipStatus __instance, SystemTypes systemType, byte amount, PlayerControl player)
    {
        if (!FixNextSabo) return;
        FixNextSabo = false;

        switch (systemType)
        {
            case SystemTypes.Reactor:
                if (amount is 64 or 65)
                {
                    ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Reactor, 16);
                    ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Reactor, 17);
                }
                break;
            case SystemTypes.Laboratory:
                if (amount is 64 or 65)
                {
                    ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Laboratory, 67);
                    ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Laboratory, 66);
                }
                break;
            case SystemTypes.LifeSupp:
                if (amount is 64 or 65)
                {
                    ShipStatus.Instance.RpcUpdateSystem(SystemTypes.LifeSupp, 67);
                    ShipStatus.Instance.RpcUpdateSystem(SystemTypes.LifeSupp, 66);
                }
                break;
            case SystemTypes.Comms:
                if (amount is 64 or 65)
                {
                    ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Comms, 16);
                    ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Comms, 17);
                }
                break;
        }
    }
    public override void SwitchSystemUpdate(SwitchSystem __instance, byte amount, PlayerControl player)
    {
        __instance.ActualSwitches = 0;
        __instance.ExpectedSwitches = 0;

        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()} instant - fix-lights", "SwitchSystem");
    }

    public override void SetAbilityButtonText(HudManager hud, byte playerId)
    {
        hud.AbilityButton.OverrideText(GetString("AlchemistVentButtonText"));
    }
}
