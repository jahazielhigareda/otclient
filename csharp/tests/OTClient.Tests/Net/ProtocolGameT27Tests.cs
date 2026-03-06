using System.Collections.Generic;
using OTClient.Framework.Game;
using OTClient.Framework.Net;
using Xunit;

namespace OTClient.Tests.Net;

/// <summary>
/// Tests for T27 parse handlers:
/// parsePreyData, parseForgeResult, parseBestiaryRaces/Overview/MonsterData,
/// parseOpenWheelWindow, parseImbuementDurations.
/// </summary>
public sealed class ProtocolGameT27Tests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static void InvokeHandleRawData(ProtocolGame pg, byte[] data)
    {
        var method = typeof(Protocol).GetMethod(
            "HandleRawData",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method!.Invoke(pg, [data]);
    }

    /// <summary>Writes a zero-look (item outfit) so ReadOutfit consumes it cleanly.</summary>
    private static void WriteZeroOutfit(OutputMessage out_)
    {
        out_.WriteU16(0); // lookType = 0 → item look branch; ReadOutfit reads U16 item id
        out_.WriteU16(0); // itemId = 0
    }

    // ─── parsePreyFreeRerolls ─────────────────────────────────────────────────

    [Fact]
    public void ParsePreyFreeRerolls_InvokesEvent()
    {
        using var pg = new ProtocolGame();
        byte receivedSlot = 0xFF;
        ushort receivedTime = 0;
        pg.PreyFreeRerollsReceived += (slot, time) => { receivedSlot = slot; receivedTime = time; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.PreyFreeRerolls);
        out_.WriteU8(2);        // slot
        out_.WriteU16(3600);    // timeLeft

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(2, receivedSlot);
        Assert.Equal(3600, receivedTime);
    }

    // ─── parsePreyTimeLeft ────────────────────────────────────────────────────

    [Fact]
    public void ParsePreyTimeLeft_InvokesEvent()
    {
        using var pg = new ProtocolGame();
        byte receivedSlot = 0xFF;
        ushort receivedTime = 0;
        pg.PreyTimeLeftReceived += (slot, time) => { receivedSlot = slot; receivedTime = time; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.PreyTimeLeft);
        out_.WriteU8(1);
        out_.WriteU16(7200);

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(1, receivedSlot);
        Assert.Equal(7200, receivedTime);
    }

    // ─── parsePreyData (Locked state) ────────────────────────────────────────

    [Fact]
    public void ParsePreyData_Locked_InvokesEvent()
    {
        using var pg = new ProtocolGame();
        PreyData? received = null;
        pg.PreyDataReceived += d => received = d;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.PreyData);
        out_.WriteU8(0);                     // slot 0
        out_.WriteU8((byte)PreyState.Locked);
        out_.WriteU8(1);                     // unlockState
        out_.WriteU32(86400);               // nextFreeReroll
        out_.WriteU8(5);                     // wildcards

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.NotNull(received);
        Assert.Equal(0, received!.Slot);
        Assert.Equal(PreyState.Locked, received.State);
        Assert.Equal(86400u, received.NextFreeReroll);
        Assert.Equal(5, received.Wildcards);
    }

    // ─── parsePreyData (Inactive state) ──────────────────────────────────────

    [Fact]
    public void ParsePreyData_Inactive_InvokesEvent()
    {
        using var pg = new ProtocolGame();
        PreyData? received = null;
        pg.PreyDataReceived += d => received = d;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.PreyData);
        out_.WriteU8(1);                       // slot 1
        out_.WriteU8((byte)PreyState.Inactive);
        out_.WriteU32(43200);                 // nextFreeReroll
        out_.WriteU8(3);                       // wildcards

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.NotNull(received);
        Assert.Equal(PreyState.Inactive, received!.State);
        Assert.Equal(43200u, received.NextFreeReroll);
    }

    // ─── parsePreyData (Active state) ────────────────────────────────────────

    [Fact]
    public void ParsePreyData_Active_InvokesEvent()
    {
        using var pg = new ProtocolGame();
        PreyData? received = null;
        pg.PreyDataReceived += d => received = d;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.PreyData);
        out_.WriteU8(0);                      // slot 0
        out_.WriteU8((byte)PreyState.Active);
        out_.WriteString("Dragon");           // monster name
        WriteZeroOutfit(out_);                // outfit
        out_.WriteU8(3);                      // bonusType
        out_.WriteU16(10);                    // bonusValue
        out_.WriteU8(2);                      // bonusGrade
        out_.WriteU16(1800);                  // timeLeft
        out_.WriteU32(0);                     // nextFreeReroll
        out_.WriteU8(0);                      // option toggle

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.NotNull(received);
        Assert.Equal(PreyState.Active, received!.State);
        Assert.Equal("Dragon", received.ActiveMonster?.Name);
        Assert.Equal(3, received.BonusType);
        Assert.Equal(10, received.BonusValue);
        Assert.Equal(1800, received.TimeLeft);
    }

    // ─── parsePreyRerollPrice ─────────────────────────────────────────────────

    [Fact]
    public void ParsePreyRerollPrice_InvokesEvent()
    {
        using var pg = new ProtocolGame();
        uint recPrice = 0, recWild = 0;
        pg.PreyRerollPriceReceived += (p, w) => { recPrice = p; recWild = w; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.PreyRerollPrice);
        out_.WriteU32(5000);   // price
        out_.WriteU32(2000);   // wildcard price

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(5000u, recPrice);
        Assert.Equal(2000u, recWild);
    }

    // ─── parseForgeResult ────────────────────────────────────────────────────

    [Fact]
    public void ParseForgeResult_FusionAction_InvokesEvent()
    {
        using var pg = new ProtocolGame();
        ForgeResult? received = null;
        pg.ForgeResultReceived += r => received = r;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.ForgeResult);
        out_.WriteU8(0);        // actionType = 0 (fusion)
        out_.WriteU8(0);        // convergence = false
        out_.WriteU8(1);        // success = true
        out_.WriteU16(2400);    // leftItemId
        out_.WriteU8(3);        // leftTier
        out_.WriteU16(2401);    // rightItemId
        out_.WriteU8(3);        // rightTier
        out_.WriteU8(0);        // bonus = 0 (no core, no leftItem swap)

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.NotNull(received);
        Assert.True(received!.Success);
        Assert.False(received.Convergence);
        Assert.Equal(2400, received.LeftItemId);
        Assert.Equal(3, received.LeftTier);
        Assert.Equal(2401, received.RightItemId);
    }

    [Fact]
    public void ParseForgeResult_TransferAction_ConsumesBonus()
    {
        using var pg = new ProtocolGame();
        ForgeResult? received = null;
        pg.ForgeResultReceived += r => received = r;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.ForgeResult);
        out_.WriteU8(1);        // actionType = 1 (transfer)
        out_.WriteU8(0);        // convergence
        out_.WriteU8(1);        // success
        out_.WriteU16(2400);    // leftItemId
        out_.WriteU8(1);        // leftTier
        out_.WriteU16(2402);    // rightItemId
        out_.WriteU8(0);        // rightTier
        out_.WriteU8(0);        // bonus type for transfer (always none, consumed)

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.NotNull(received);
        Assert.Equal(1, received!.ActionType);
    }

    // ─── parseBestiaryRaces ───────────────────────────────────────────────────

    [Fact]
    public void ParseBestiaryRaces_InvokesEvent_WithRaces()
    {
        using var pg = new ProtocolGame();
        IReadOnlyList<BestiaryRace>? received = null;
        pg.BestiaryRacesReceived += r => received = r;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.BestiaryRaces);
        out_.WriteU16(2);               // 2 races
        out_.WriteString("Mammals");    // race 0
        out_.WriteU16(150);             // count
        out_.WriteU16(80);              // unlockedCount
        out_.WriteString("Dragons");    // race 1
        out_.WriteU16(30);
        out_.WriteU16(12);

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.NotNull(received);
        Assert.Equal(2, received!.Count);
        Assert.Equal("Mammals", received[0].ClassName);
        Assert.Equal(150, received[0].Count);
        Assert.Equal(80, received[0].UnlockedCount);
        Assert.Equal("Dragons", received[1].ClassName);
    }

    // ─── parseBestiaryOverview ────────────────────────────────────────────────

    [Fact]
    public void ParseBestiaryOverview_InvokesEvent()
    {
        using var pg = new ProtocolGame();
        string? raceName = null;
        IReadOnlyList<BestiaryMonster>? monsters = null;
        pg.BestiaryOverviewReceived += (r, m, _) => { raceName = r; monsters = m; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.BestiaryOverview);
        out_.WriteString("Mammals");    // raceName
        out_.WriteU16(1);              // 1 monster
        out_.WriteU16(78);             // id
        out_.WriteU8(2);               // currentLevel (> 0 → occurrence byte follows)
        out_.WriteU8(1);               // occurrence
        out_.WriteU16(100);            // animusMasteryBonus
        out_.WriteU16(500);            // animusMasteryPoints

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal("Mammals", raceName);
        Assert.NotNull(monsters);
        Assert.Single(monsters!);
        Assert.Equal(78, monsters![0].Id);
        Assert.Equal(2, monsters[0].CurrentLevel);
    }

    // ─── parseBestiaryMonsterData ─────────────────────────────────────────────

    [Fact]
    public void ParseBestiaryMonsterData_Level1_InvokesEvent()
    {
        using var pg = new ProtocolGame();
        BestiaryMonsterData? received = null;
        pg.BestiaryMonsterDataReceived += d => received = d;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.BestiaryMonsterData);
        out_.WriteU16(78);              // id
        out_.WriteString("Rabbit");     // class
        out_.WriteU8(1);                // currentLevel
        out_.WriteU16(0);               // animusMasteryBonus
        out_.WriteU16(0);               // animusMasteryPoints
        out_.WriteU32(100);             // killCounter
        out_.WriteU16(500);             // thirdDifficulty
        out_.WriteU16(200);             // secondUnlock
        out_.WriteU16(50);              // lastProgressKillCount
        out_.WriteU8(2);                // difficulty
        out_.WriteU8(1);                // occurrence
        out_.WriteU8(1);                // lootCount = 1
        // loot item
        out_.WriteU16(2148);            // itemId (non-zero)
        out_.WriteU8(1);                // difficulty
        out_.WriteU8(0);                // specialEvent
        out_.WriteString("Meat");       // name
        out_.WriteU8(1);                // amount
        // No level-2 or level-3 fields because currentLevel == 1

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.NotNull(received);
        Assert.Equal(78, received!.Id);
        Assert.Equal("Rabbit", received.ClassName);
        Assert.Equal(100u, received.KillCounter);
        Assert.Single(received.Loot);
        Assert.Equal("Meat", received.Loot[0].Name);
    }

    // ─── parseImbuementDurations ──────────────────────────────────────────────

    [Fact]
    public void ParseImbuementDurations_InvokesEvent()
    {
        using var pg = new ProtocolGame();
        IReadOnlyList<ImbuementTrackerItem>? received = null;
        pg.ImbuementDurationsReceived += list => received = list;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.ImbuementDurations);
        out_.WriteU8(1);                // 1 tracker item
        out_.WriteU8(0);                // trackSlot
        out_.WriteU16(2400);            // item id (ReadItemById reads U16)
        out_.WriteU8(2);                // totalSlots = 2
        // slot 0: imbued = true
        out_.WriteU8(1);
        out_.WriteString("Powerful Strike");
        out_.WriteU16(10);              // iconId
        out_.WriteU32(3600);            // duration
        out_.WriteU8(1);                // state = decaying
        // slot 1: imbued = false (no further bytes)
        out_.WriteU8(0);

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.NotNull(received);
        Assert.Single(received!);
        var item = received![0];
        Assert.Equal(0, item.TrackSlot);
        Assert.Equal(2, item.TotalSlots);
        Assert.Single(item.Slots);
        Assert.Equal("Powerful Strike", item.Slots[0].Name);
        Assert.Equal(3600u, item.Slots[0].Duration);
    }

    // ─── parseOpenWheelWindow ────────────────────────────────────────────────

    [Fact]
    public void ParseOpenWheelWindow_WithCanView_InvokesEvent()
    {
        using var pg = new ProtocolGame();
        WheelData? received = null;
        pg.WheelWindowReceived += d => received = d;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.OpenWheelWindow);
        out_.WriteU32(12345);   // playerId
        out_.WriteU8(1);        // canView = true
        out_.WriteU8(0);        // changeState
        out_.WriteU8(4);        // vocationId
        out_.WriteU16(100);     // points
        out_.WriteU16(10);      // extraPoints

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.NotNull(received);
        Assert.Equal(12345u, received!.PlayerId);
        Assert.True(received.CanView);
        Assert.Equal(4, received.VocationId);
        Assert.Equal(100, received.Points);
        Assert.Equal(10, received.ExtraPoints);
    }

    [Fact]
    public void ParseOpenWheelWindow_WithoutCanView_InvokesEventWithFalse()
    {
        using var pg = new ProtocolGame();
        WheelData? received = null;
        pg.WheelWindowReceived += d => received = d;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.OpenWheelWindow);
        out_.WriteU32(99999);
        out_.WriteU8(0);        // canView = false → no further bytes

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.NotNull(received);
        Assert.False(received!.CanView);
        Assert.Equal(99999u, received.PlayerId);
    }
}
