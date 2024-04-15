using AmongUs.GameOptions;
using TOHE.Roles.Double;
using static TOHE.Options;
using UnityEngine;

namespace TOHE.Roles.Neutral;

internal class Werewolf : RoleBase
{
    //===========================SETUP================================\\
    private const int Id = 18400;
    private static readonly HashSet<byte> playerIdList = [];
    public static bool HasEnabled => playerIdList.Any();
    public override bool IsEnable => HasEnabled;
    public override CustomRoles ThisRoleBase => CustomRoles.Impostor;
    public override Custom_RoleType ThisRoleType => Custom_RoleType.NeutralKilling;
    //==================================================================\\

    private static OptionItem KillCooldown;
    private static OptionItem MaulRadius;
    private static OptionItem CanVent;
    private static OptionItem HasImpostorVision;

    public override void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Werewolf, 1, zeroOne: false);
        KillCooldown = FloatOptionItem.Create(Id + 9, "KillCooldown", new(0f, 180f, 2.5f), 35f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Werewolf])
            .SetValueFormat(OptionFormat.Seconds);
        MaulRadius = FloatOptionItem.Create(Id + 14, "MaulRadius", new(0.5f, 1.5f, 0.1f), 1.3f, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Werewolf])
            .SetValueFormat(OptionFormat.Multiplier);
        CanVent = BooleanOptionItem.Create(Id + 11, "CanVent", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Werewolf]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Werewolf]);
    }
    public override void Init()
    {
        playerIdList.Clear();
    }
    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());
    
    public override bool CanUseKillButton(PlayerControl pc) => true;
    public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();

    public override bool OnCheckMurderAsKiller(PlayerControl killer, PlayerControl target)
    {
        Logger.Info("Werewolf Kill", "Mauled");
        _ = new LateTask(() =>
        {
            foreach (var player in Main.AllAlivePlayerControls)
            {
                if (player == killer) continue;
                if (player == target) continue;

                if (player.Is(CustomRoles.Pestilence)) continue;
                else if ((player.Is(CustomRoles.NiceMini) || player.Is(CustomRoles.EvilMini)) && Mini.Age < 18) continue;

                if (Vector2.Distance(killer.transform.position, player.transform.position) <= Werewolf.MaulRadius.GetFloat())
                {
                    Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.Mauled;
                    player.SetRealKiller(killer);
                    player.RpcMurderPlayer(player);
                }
            }
        }, 0.1f, "Werewolf Maul Bug Fix");
        return true;
    }
}
