﻿using AmongUs.GameOptions;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.MeetingHudStartPatch;

//Thanks TOH_Y
namespace TOHE.Roles.Neutral;

internal class Workaholic : RoleBase
{
    //===========================SETUP================================\\
    private const int Id = 15800;
    private static readonly HashSet<byte> PlayerIds = [];
    public static bool HasEnabled => PlayerIds.Any();
    public override bool IsEnable => HasEnabled;
    public override CustomRoles ThisRoleBase => CustomRoles.Engineer;
    //==================================================================\\
    public override bool HasTasks(GameData.PlayerInfo player, CustomRoles role, bool ForRecompute) => !ForRecompute;

    public static OptionItem WorkaholicCannotWinAtDeath;
    public static OptionItem WorkaholicVentCooldown;
    public static OptionItem WorkaholicVisibleToEveryone;
    public static OptionItem WorkaholicGiveAdviceAlive;
    public static OptionItem WorkaholicCanGuess;

    public static readonly HashSet<byte> WorkaholicAlive = [];

    public static void SetupCustomOptions()
    {
        SetupRoleOptions(15700, TabGroup.NeutralRoles, CustomRoles.Workaholic); //TOH_Y
        WorkaholicCannotWinAtDeath = BooleanOptionItem.Create(15702, "WorkaholicCannotWinAtDeath", false, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Workaholic]);
        WorkaholicVentCooldown = FloatOptionItem.Create(15703, "VentCooldown", new(0f, 180f, 2.5f), 0f, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Workaholic])
            .SetValueFormat(OptionFormat.Seconds);
        WorkaholicVisibleToEveryone = BooleanOptionItem.Create(15704, "WorkaholicVisibleToEveryone", true, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Workaholic]);
        WorkaholicGiveAdviceAlive = BooleanOptionItem.Create(15705, "WorkaholicGiveAdviceAlive", true, TabGroup.NeutralRoles, false)
            .SetParent(WorkaholicVisibleToEveryone);
        WorkaholicCanGuess = BooleanOptionItem.Create(15706, "CanGuess", true, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Workaholic]);
        OverrideTasksData.Create(15707, TabGroup.NeutralRoles, CustomRoles.Workaholic);
    }
    public override void Init()
    {
        WorkaholicAlive.Clear();
        PlayerIds.Clear();
    }
    public override void Add(byte playerId)
    {
        PlayerIds.Add(playerId);
    }
    
    public static bool OthersKnowWorka(PlayerControl target)
        => WorkaholicVisibleToEveryone.GetBool() && target.Is(CustomRoles.Workaholic);

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.EngineerCooldown = WorkaholicVentCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 0f;
    }
    public override void OnTaskComplete(PlayerControl player, int completedTaskCount, int totalTaskCount)
    {
        var AllTasksCount = player.Data.Tasks.Count;
        if (!((completedTaskCount + 1) >= AllTasksCount && !(WorkaholicCannotWinAtDeath.GetBool() && !player.IsAlive()))) return;

        Logger.Info("The Workaholic task is done", "Workaholic");

        RPC.PlaySoundRPC(player.PlayerId, Sounds.KillSound);
        foreach (var pc in Main.AllAlivePlayerControls)
        {
            if (pc.PlayerId != player.PlayerId)
            {
                Main.PlayerStates[pc.PlayerId].deathReason = pc.PlayerId == player.PlayerId ?
                    PlayerState.DeathReason.Overtired : PlayerState.DeathReason.Ashamed;

                pc.RpcMurderPlayer(pc);
                pc.SetRealKiller(player);
            }
        }

        if (!CustomWinnerHolder.CheckForConvertedWinner(player.PlayerId))
        {
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Workaholic); //Workaholic win
            CustomWinnerHolder.WinnerIds.Add(player.PlayerId);
        }

    }
    public override void OnMeetingHudStart(PlayerControl player)
    {
        if (MeetingStates.FirstMeeting && player.IsAlive() && WorkaholicGiveAdviceAlive.GetBool() && !WorkaholicCannotWinAtDeath.GetBool() && !GhostIgnoreTasks.GetBool())
        {
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.Is(CustomRoles.Workaholic)).ToArray())
            {
                WorkaholicAlive.Add(pc.PlayerId);
            }
            List<string> workaholicAliveList = [];
            foreach (var whId in WorkaholicAlive.ToArray())
            {
                workaholicAliveList.Add(Main.AllPlayerNames[whId]);
            }
            string separator = TranslationController.Instance.currentLanguage.languageID is SupportedLangs.English or SupportedLangs.Russian ? "], [" : "】, 【";
            AddMsg(string.Format(GetString("WorkaholicAdviceAlive"), string.Join(separator, workaholicAliveList)), 255, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Workaholic), GetString("WorkaholicAliveTitle")));
        }
    }
    public override bool OnRoleGuess(bool isUI, PlayerControl target, PlayerControl pc, CustomRoles role, ref bool guesserSuicide)
    {
        if(WorkaholicVisibleToEveryone.GetBool())
        {
            if (!isUI) Utils.SendMessage(GetString("GuessWorkaholic"), pc.PlayerId);
            else pc.ShowPopUp(GetString("GuessWorkaholic"));
            return true;
        }
        return false;
    }
    public override bool GuessCheck(bool isUI, PlayerControl guesser, PlayerControl pc, CustomRoles role, ref bool guesserSuicide)
    {
        if (!WorkaholicCanGuess.GetBool())
        {
            if (!isUI) Utils.SendMessage(GetString("GuessDisabled"), pc.PlayerId);
            else pc.ShowPopUp(GetString("GuessDisabled"));
            return false;
        }
        return true;
    }
}
