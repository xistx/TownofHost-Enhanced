using AmongUs.GameOptions;
using Hazel;
using System;
using System.Text;
using UnityEngine;
using TOHE.Roles.Core;
using static TOHE.Utils;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate;

internal class Chameleon : RoleBase
{
    //===========================SETUP================================\\
    private const int Id = 7600;
    public static bool HasEnabled => CustomRoleManager.HasEnabled(CustomRoles.Chameleon);
    public override CustomRoles ThisRoleBase => CustomRoles.Engineer;
    public override Custom_RoleType ThisRoleType => Custom_RoleType.CrewmateSupport;
    //==================================================================\\

    private static OptionItem ChameleonCooldown;
    private static OptionItem ChameleonDuration;
    private static OptionItem UseLimitOpt;
    private static OptionItem ChameleonAbilityUseGainWithEachTaskCompleted;

    private static Dictionary<byte, long> InvisTime = [];
    private static readonly Dictionary<byte, long> lastTime = [];
    private static readonly Dictionary<byte, int> ventedId = [];

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Chameleon);
        ChameleonCooldown = FloatOptionItem.Create(Id + 2, "ChameleonCooldown", new(1f, 60f, 1f), 30f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Chameleon])
            .SetValueFormat(OptionFormat.Seconds);
        ChameleonDuration = FloatOptionItem.Create(Id + 4, "ChameleonDuration", new(1f, 30f, 1f), 15f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Chameleon])
            .SetValueFormat(OptionFormat.Seconds);
        UseLimitOpt = IntegerOptionItem.Create(Id + 5, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Chameleon])
            .SetValueFormat(OptionFormat.Times);
        ChameleonAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 6, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 1f, TabGroup.CrewmateRoles, false)
        .SetParent(CustomRoleSpawnChances[CustomRoles.Chameleon])
        .SetValueFormat(OptionFormat.Times);
    }
    public override void Init()
    {
        InvisTime.Clear();
        lastTime.Clear();
        ventedId.Clear();
    }
    public override void Add(byte playerId)
    {
        AbilityLimit = UseLimitOpt.GetInt();
    }
    public void SendRPC(PlayerControl pc, bool isLimit = false)
    {
        if (isLimit)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetChameleonTimer, SendOption.Reliable, -1);
            writer.Write(pc.PlayerId);
            writer.Write(isLimit);
            writer.Write(AbilityLimit);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        else 
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetChameleonTimer, SendOption.Reliable, pc.GetClientId());
            writer.Write(pc.PlayerId);
            writer.Write(isLimit);
            writer.Write((InvisTime.TryGetValue(pc.PlayerId, out var x) ? x : -1).ToString());
            writer.Write((lastTime.TryGetValue(pc.PlayerId, out var y) ? y : -1).ToString());
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }
    public static void ReceiveRPC_Custom(MessageReader reader)
    {
        byte pid = reader.ReadByte();
        bool isLimit = reader.ReadBoolean();
        if (isLimit)
        {
            float limit = reader.ReadSingle();
            Main.PlayerStates[pid].RoleClass.AbilityLimit = limit;
        }
        else 
        {
            InvisTime.Clear();
            lastTime.Clear();
            long invis = long.Parse(reader.ReadString());
            long last = long.Parse(reader.ReadString());
            if (invis > 0) InvisTime.Add(pid, invis);
            if (last > 0) lastTime.Add(pid, last);
        }
    }
    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.EngineerCooldown = ChameleonCooldown.GetFloat() + 1f;
        AURoleOptions.EngineerInVentMaxTime = 1f;
    }
    private static bool CanGoInvis(byte id)
        => GameStates.IsInTask && !InvisTime.ContainsKey(id) && !lastTime.ContainsKey(id);
    private static bool IsInvis(byte id) => InvisTime.ContainsKey(id);

    private static long lastFixedTime = 0;
    public override void OnReportDeadBody(PlayerControl y, GameData.PlayerInfo x)
    {
        lastTime.Clear();
        InvisTime.Clear();

        foreach (var chameleonId in _playerIdList.ToArray())
        {
            if (!ventedId.ContainsKey(chameleonId)) continue;
            var chameleon = GetPlayerById(chameleonId);
            if (chameleon == null) return;

            chameleon?.MyPhysics?.RpcBootFromVent(ventedId.TryGetValue(chameleonId, out var id) ? id : Main.LastEnteredVent[chameleonId].Id);
            SendRPC(chameleon);
        }

        ventedId.Clear();
    }
    public override void AfterMeetingTasks()
    {
        lastTime.Clear();
        InvisTime.Clear();
        foreach (var pc in Main.AllAlivePlayerControls.Where(x => _playerIdList.Contains(x.PlayerId)).ToArray())
        {
            lastTime.Add(pc.PlayerId, GetTimeStamp());
            SendRPC(pc);
        }
    }
    public override bool OnTaskComplete(PlayerControl player, int completedTaskCount, int totalTaskCount)
    {
        if (player.IsAlive())
        {
            AbilityLimit += ChameleonAbilityUseGainWithEachTaskCompleted.GetFloat();
            SendRPC(player, isLimit: true);
        }
        return true;
    }
    public override void OnFixedUpdateLowLoad(PlayerControl player)
    {
        var now = GetTimeStamp();

        if (lastTime.TryGetValue(player.PlayerId, out var time) && time + (long)ChameleonCooldown.GetFloat() < now)
        {
            lastTime.Remove(player.PlayerId);
            if (!player.IsModClient()) player.Notify(GetString("ChameleonCanVent"));
            SendRPC(player);
        }

        if (lastFixedTime != now)
        {
            lastFixedTime = now;
            Dictionary<byte, long> newList = [];
            List<byte> refreshList = [];
            foreach (var it in InvisTime)
            {
                var pc = GetPlayerById(it.Key);
                if (pc == null) continue;
                var remainTime = it.Value + (long)ChameleonDuration.GetFloat() - now;
                if (remainTime < 0)
                {
                    lastTime.Add(pc.PlayerId, now);
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
            refreshList.Do(x => SendRPC(GetPlayerById(x)));
        }
    }
    public override void OnCoEnterVent(PlayerPhysics __instance, int ventId)
    {
        var pc = __instance.myPlayer;
        if (!AmongUsClient.Instance.AmHost || IsInvis(pc.PlayerId)) return;
        _ = new LateTask(() =>
        {
            if (CanGoInvis(pc.PlayerId))
            {
                if (AbilityLimit >= 1)
                {
                    ventedId.Remove(pc.PlayerId);
                    ventedId.Add(pc.PlayerId, ventId);

                    MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.BootFromVent, SendOption.Reliable, pc.GetClientId());
                    writer.WritePacked(ventId);
                    AmongUsClient.Instance.FinishRpcImmediately(writer);

                    InvisTime.Add(pc.PlayerId, GetTimeStamp());
                    SendRPC(pc);
                    pc.Notify(GetString("ChameleonInvisState"), ChameleonDuration.GetFloat());

                    AbilityLimit -= 1;
                    SendRPC(pc, isLimit: true);
                }
                else
                {
                    pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
                }
            }
            else
            {
                //__instance.myPlayer.MyPhysics.RpcBootFromVent(ventId);
                pc.Notify(GetString("ChameleonInvisInCooldown"));
            }
        }, 0.5f, "Chameleon Vent");
        return;
    }
    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (!CustomRoles.Chameleon.HasEnabled()) return;
        if (!pc.Is(CustomRoles.Chameleon) || !IsInvis(pc.PlayerId)) return;

        InvisTime.Remove(pc.PlayerId);
        lastTime.Add(pc.PlayerId, GetTimeStamp());
        SendRPC(pc);

        pc?.MyPhysics?.RpcBootFromVent(vent.Id);
        pc.Notify(GetString("ChameleonInvisStateOut"));
    }
    public override string GetLowerText(PlayerControl pc, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        // Only for modded
        if (pc == null || !isForHud || isForMeeting || !pc.IsAlive()) return string.Empty;

        var str = new StringBuilder();
        if (IsInvis(pc.PlayerId))
        {
            var remainTime = InvisTime[pc.PlayerId] + (long)ChameleonDuration.GetFloat() - GetTimeStamp();
            str.Append(string.Format(GetString("ChameleonInvisStateCountdown"), remainTime + 1));
        }
        else if (lastTime.TryGetValue(pc.PlayerId, out var time))
        {
            var cooldown = time + (long)ChameleonCooldown.GetFloat() - GetTimeStamp();
            str.Append(string.Format(GetString("ChameleonInvisCooldownRemain"), cooldown + 1));
        }
        else
        {
            str.Append(GetString("ChameleonCanVent"));
        }
        return str.ToString();
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        if (!IsInvis(killer.PlayerId)) return true;
        target?.MyPhysics?.RpcBootFromVent(ventedId.TryGetValue(target.PlayerId, out var id) ? id : Main.LastEnteredVent[target.PlayerId].Id);
        return true;
    }
    public override void SetAbilityButtonText(HudManager hud, byte id)
    {
        hud.AbilityButton.OverrideText(GetString(IsInvis(PlayerControl.LocalPlayer.PlayerId) ? "ChameleonRevertDisguise" : "ChameleonDisguise"));
        hud.ReportButton.OverrideText(GetString("ReportButtonText"));
    }
    public override Sprite ImpostorVentButtonSprite(PlayerControl player) => CustomButton.Get("invisible");

    public override string GetProgressText(byte playerId, bool comms)
    {
        var ProgressText = new StringBuilder();
        var taskState13 = Main.PlayerStates?[playerId].TaskState;
        Color TextColor13;
        var TaskCompleteColor13 = Color.green;
        var NonCompleteColor13 = Color.yellow;
        var NormalColor13 = taskState13.IsTaskFinished ? TaskCompleteColor13 : NonCompleteColor13;
        TextColor13 = comms ? Color.gray : NormalColor13;
        string Completed13 = comms ? "?" : $"{taskState13.CompletedTasksCount}";
        Color TextColor131;
        if (AbilityLimit < 1) TextColor131 = Color.red;
        else TextColor131 = Color.white;
        ProgressText.Append(ColorString(TextColor13, $"({Completed13}/{taskState13.AllTasksCount})"));
        ProgressText.Append(ColorString(TextColor131, $" <color=#ffffff>-</color> {Math.Round(AbilityLimit, 1)}"));
        return ProgressText.ToString();
    }
}