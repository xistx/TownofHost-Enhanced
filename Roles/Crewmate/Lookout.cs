﻿using static TOHE.Options;
using static TOHE.Utils;
using TOHE.Roles.Neutral;

namespace TOHE.Roles.Crewmate;

internal class Lookout : RoleBase
{
    //===========================SETUP================================\\
    private const int Id = 11800;
    private static readonly HashSet<byte> playerIdList = [];
    public static bool HasEnabled => playerIdList.Any();
    
    public override CustomRoles ThisRoleBase => CustomRoles.Crewmate;
    public override Custom_RoleType ThisRoleType => Custom_RoleType.CrewmatePower;
    //==================================================================\\

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Lookout);
    }
    
    public override void Init()
    {
        playerIdList.Clear();
    }
    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;

        if (!seer.IsAlive() || !seen.IsAlive()) return string.Empty;

        if (Doppelganger.CheckDoppelVictim(seen.PlayerId))
            seen = Doppelganger.GetDoppelControl(seen);

        return ColorString(GetRoleColor(CustomRoles.Lookout), " " + seen.PlayerId.ToString()) + " ";
    }
}
