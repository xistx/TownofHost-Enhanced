﻿using Hazel;
using System;
using System.Text.RegularExpressions;
using TOHE.Modules.ChatManager;
using TOHE.Roles.Core;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Double;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor;

internal class Councillor : RoleBase
{
    //===========================SETUP================================\\
    private const int Id = 1000;
    public static bool HasEnabled => CustomRoleManager.HasEnabled(CustomRoles.Councillor);
    public override CustomRoles ThisRoleBase => CustomRoles.Impostor;
    public override Custom_RoleType ThisRoleType => Custom_RoleType.ImpostorKilling;
    //==================================================================\\

    private static OptionItem MurderLimitPerMeeting;
    private static OptionItem MurderLimitPerGame;
    private static OptionItem TryHideMsg;
    private static OptionItem CanMurderMadmate;
    private static OptionItem CanMurderImpostor;
    private static OptionItem KillCooldown;
    
    private int MurderLimitMeeting;


    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Councillor);
            KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Councillor])
                .SetValueFormat(OptionFormat.Seconds);
        MurderLimitPerMeeting = IntegerOptionItem.Create(Id + 11, "CouncillorMurderLimitPerMeeting", new(1, 15, 1), 1, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Councillor])
            .SetValueFormat(OptionFormat.Times);
        MurderLimitPerGame = IntegerOptionItem.Create(Id + 12, "CouncillorMurderLimitPerGame", new(1, 15, 1), 4, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Councillor])
            .SetValueFormat(OptionFormat.Times);
        CanMurderMadmate = BooleanOptionItem.Create(Id + 13, "CouncillorCanMurderMadmate", true, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Councillor]);
        CanMurderImpostor = BooleanOptionItem.Create(Id + 14, "CouncillorCanMurderImpostor", true, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Councillor]);
        TryHideMsg = BooleanOptionItem.Create(Id + 15, "CouncillorTryHideMsg", true, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Councillor])
            .SetColor(Color.green);
    }
    public override void Add(byte playerId)
    {
        MurderLimitMeeting = MurderLimitPerMeeting.GetInt();
        AbilityLimit = MurderLimitPerGame.GetInt();
    }
    public override void AfterMeetingTasks()
    {
        MurderLimitMeeting = MurderLimitPerMeeting.GetInt();
    }

    public override string NotifyPlayerName(PlayerControl seer, PlayerControl target, string TargetPlayerName = "", bool IsForMeeting = false)
        => IsForMeeting && seer.IsAlive() && target.IsAlive() ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Councillor), target.PlayerId.ToString()) + " " + TargetPlayerName : string.Empty;

    public override string PVANameText(PlayerVoteArea pva, PlayerControl seer, PlayerControl target)
        => seer.IsAlive() && target.IsAlive() ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Councillor), target.PlayerId.ToString()) + " " + pva.NameText.text : string.Empty;

    public bool MurderMsg(PlayerControl pc, string msg, bool isUI = false)
    {
        var originMsg = msg;

        if (!AmongUsClient.Instance.AmHost) return false;
        if (!GameStates.IsMeeting || _Player == null || GameStates.IsExilling) return false;

        int operate = 0; // 1:ID 2:猜测
        msg = msg.ToLower().TrimStart().TrimEnd();
        if (CheckCommond(ref msg, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id")) operate = 1;
        else if (CheckCommond(ref msg, "sp|jj|tl|Murder|审判|判|审", false)) operate = 2;
        else return false;

        if (!pc.IsAlive())
        {
            Utils.SendMessage(GetString("CouncillorDead"), pc.PlayerId);
            return true;
        }

        if (operate == 1)
        {
            Utils.SendMessage(GuessManager.GetFormatString(), pc.PlayerId);
            return true;
        }
        else if (operate == 2)
        {

            if (TryHideMsg.GetBool())
            {
                //if (Options.NewHideMsg.GetBool()) ChatManager.SendPreviousMessagesToAll();
                //else GuessManager.TryHideMsg();
                GuessManager.TryHideMsg();
                ChatManager.SendPreviousMessagesToAll();
            }
            else if (pc.AmOwner) Utils.SendMessage(originMsg, 255, pc.GetRealName());

            if (!MsgToPlayerAndRole(msg, out byte targetId, out string error))
            {
                Utils.SendMessage(error, pc.PlayerId);
                return true;
            }
            var target = Utils.GetPlayerById(targetId);
            if (target != null)
            {
                Logger.Info($"{pc.GetNameWithRole()} 审判了 {target.GetNameWithRole()}", "Councillor");
                bool CouncillorSuicide = true;
                if (MurderLimitMeeting <= 0)
                {
                    if (!isUI) Utils.SendMessage(GetString("CouncillorMurderMaxMeeting"), pc.PlayerId);
                    else pc.ShowPopUp(GetString("CouncillorMurderMaxMeeting"));
                    return true;
                }
                else if (AbilityLimit <= 0)
                {
                    if (!isUI) Utils.SendMessage(GetString("CouncillorMurderMaxGame"), pc.PlayerId);
                    else pc.ShowPopUp(GetString("CouncillorMurderMaxGame"));
                    return true;
                }
                if (Jailer.IsTarget(target.PlayerId))
                {
                    if (!isUI) Utils.SendMessage(GetString("CanNotTrialJailed"), pc.PlayerId, title: Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailer), GetString("JailerTitle")));
                    else pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailer), GetString("JailerTitle")) + "\n" + GetString("CanNotTrialJailed"));
                    return true;
                }
                if (pc.PlayerId == target.PlayerId)
                {
                    if (!isUI) Utils.SendMessage(GetString("Councillor_LaughToWhoMurderSelf"), pc.PlayerId, Utils.ColorString(Color.cyan, GetString("MessageFromKPD")));
                    else pc.ShowPopUp(Utils.ColorString(Color.cyan, GetString("MessageFromKPD")) + "\n" + GetString("Councillor_LaughToWhoMurderSelf"));
                    CouncillorSuicide = true;
                }
                if (target.Is(CustomRoles.NiceMini) && Mini.Age < 18)
                {
                    if (!isUI) Utils.SendMessage(GetString("GuessMini"), pc.PlayerId);
                    else pc.ShowPopUp(GetString("GuessMini"));
                    return true;
                }
                else if (target.Is(CustomRoles.Rebound))
                {
                    Logger.Info($"{pc.GetNameWithRole()} judged {target.GetNameWithRole()}, councillor sucide = true because target rebound", "CouncillorTrialMsg");
                    CouncillorSuicide = true;
                }
                else if (target.Is(CustomRoles.Solsticer))
                {
                    if (!isUI) Utils.SendMessage(GetString("GuessSolsticer"), pc.PlayerId);
                    else pc.ShowPopUp(GetString("GuessSolsticer"));
                    return true;
                }
                else if (target.Is(CustomRoles.Madmate) && CanMurderMadmate.GetBool()) CouncillorSuicide = false;
                else if (target.Is(CustomRoles.Parasite) && CanMurderMadmate.GetBool()) CouncillorSuicide = false;
                else if (target.Is(CustomRoles.Refugee) && CanMurderMadmate.GetBool()) CouncillorSuicide = false;
                else if (target.Is(CustomRoles.Crewpostor) && CanMurderMadmate.GetBool()) CouncillorSuicide = false;
                else if (target.Is(CustomRoles.SuperStar)) CouncillorSuicide = true;
                else if (target.Is(CustomRoles.Pestilence)) CouncillorSuicide = true;
                else if (target.Is(CustomRoles.Marshall) && target.AllTasksCompleted()) CouncillorSuicide = true;
                else if (target.Is(CustomRoles.Snitch) && target.AllTasksCompleted()) CouncillorSuicide = true;
                else if (Guardian.CannotBeKilled(target)) CouncillorSuicide = true;
                else if (target.Is(CustomRoles.Merchant) && Merchant.IsBribedKiller(pc, target)) CouncillorSuicide = true;
                else if ((target.GetCustomRole().IsImpostor()) && CanMurderImpostor.GetBool()) CouncillorSuicide = false;
                else if (target.GetCustomRole().IsCrewmate()) CouncillorSuicide = false;
                else if (target.GetCustomRole().IsNeutral()) CouncillorSuicide = false;
                else CouncillorSuicide = true;

                var dp = CouncillorSuicide ? pc : target;
                target = dp;

                string Name = dp.GetRealName();

                MurderLimitMeeting--;
                AbilityLimit--;
                SendSkillRPC();

                if (!GameStates.IsProceeding)
                _ = new LateTask(() =>
                {
                    Main.PlayerStates[dp.PlayerId].deathReason = PlayerState.DeathReason.Trialed;
                    dp.SetRealKiller(pc);
                    GuessManager.RpcGuesserMurderPlayer(dp);

                    //死者检查
                    MurderPlayerPatch.AfterPlayerDeathTasks(pc, dp, true);

                    Utils.NotifyRoles(isForMeeting: false, NoCache: true);

                    _ = new LateTask(() => { Utils.SendMessage(string.Format(GetString("Councillor_MurderKill"), Name), 255, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Judge), GetString("Councillor_MurderKillTitle")), true); }, 0.6f, "Guess Msg");

                }, 0.2f, "Murder Kill");
            }
        }
        return true;
    }
    private static bool MsgToPlayerAndRole(string msg, out byte id, out string error)
    {
        if (msg.StartsWith("/")) msg = msg.Replace("/", string.Empty);

        Regex r = new("\\d+");
        MatchCollection mc = r.Matches(msg);
        string result = string.Empty;
        for (int i = 0; i < mc.Count; i++)
        {
            result += mc[i];//匹配结果是完整的数字，此处可以不做拼接的
        }

        if (int.TryParse(result, out int num))
        {
            id = Convert.ToByte(num);
        }
        else
        {
            //并不是玩家编号，判断是否颜色
            //byte color = GetColorFromMsg(msg);
            //好吧我不知道怎么取某位玩家的颜色，等会了的时候再来把这里补上
            id = byte.MaxValue;
            error = GetString("Councillor_MurderHelp");
            return false;
        }

        //判断选择的玩家是否合理
        PlayerControl target = Utils.GetPlayerById(id);
        if (target == null || target.Data.IsDead)
        {
            error = GetString("Councillor_MurderNull");
            return false;
        }

        error = string.Empty;
        return true;
    }
    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();

    private static bool CheckCommond(ref string msg, string command, bool exact = true)
    {
        var comList = command.Split('|');
        for (int i = 0; i < comList.Length; i++)
        {
            if (exact)
            {
                if (msg == "/" + comList[i]) return true;
            }
            else
            {
                if (msg.StartsWith("/" + comList[i]))
                {
                    msg = msg.Replace("/" + comList[i], string.Empty);
                    return true;
                }
            }
        }
        return false;
    }

    private static void SendRPC(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.CouncillorJudge, SendOption.Reliable, -1);
        writer.Write(playerId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC_Custom(MessageReader reader, PlayerControl pc)
    {
        int PlayerId = reader.ReadByte();
        if (pc.GetRoleClass() is Councillor cl) cl.MurderMsg(pc, $"/tl {PlayerId}", true);
    }

    private static void CouncillorOnClick(byte playerId/*, MeetingHud __instance*/)
    {
        Logger.Msg($"Click: ID {playerId}", "Councillor UI");
        var pc = Utils.GetPlayerById(playerId);
        if (pc == null || !pc.IsAlive() || !GameStates.IsVoting) return;
        if (AmongUsClient.Instance.AmHost && pc.GetRoleClass() is Councillor cl) cl.MurderMsg(PlayerControl.LocalPlayer, $"/tl {playerId}", true);
        else SendRPC(playerId);
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    class StartMeetingPatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            if (PlayerControl.LocalPlayer.Is(CustomRoles.Councillor) && PlayerControl.LocalPlayer.IsAlive())
                CreateCouncillorButton(__instance);
        }
    }
    private static void CreateCouncillorButton(MeetingHud __instance)
    {
        foreach (var pva in __instance.playerStates)
        {
            var pc = Utils.GetPlayerById(pva.TargetPlayerId);
            if (pc == null || !pc.IsAlive()) continue;
            GameObject template = pva.Buttons.transform.Find("CancelButton").gameObject;
            GameObject targetBox = UnityEngine.Object.Instantiate(template, pva.transform);
            targetBox.name = "ShootButton";
            targetBox.transform.localPosition = new Vector3(-0.35f, 0.03f, -1.31f);
            SpriteRenderer renderer = targetBox.GetComponent<SpriteRenderer>();
            renderer.sprite = CustomButton.Get("MeetingKillButton");
            PassiveButton button = targetBox.GetComponent<PassiveButton>();
            button.OnClick.RemoveAllListeners();
            button.OnClick.AddListener((Action)(() => CouncillorOnClick(pva.TargetPlayerId/*, __instance*/)));
        }
    }

    public override string GetProgressText(byte playerId, bool coooms)
    {
        var player = Utils.GetPlayerById(playerId);
        if (player == null) return "Invalid";
        return Utils.ColorString(AbilityLimit <= 0 ? Color.gray : Utils.GetRoleColor(CustomRoles.Councillor), $"({AbilityLimit})");
    }
}