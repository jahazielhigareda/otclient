using System.Collections.Generic;
using OTClient.Framework.Game;
using OTClient.Framework.Net;
using Xunit;

namespace OTClient.Tests.Net;

/// <summary>
/// Tests for T28 parse handlers:
/// parseCoinBalance, parseStoreError, parseCoinBalanceUpdating,
/// parseStore, parseStoreOffers, parseStoreTransactionHistory,
/// parseCompleteStorePurchase.
/// </summary>
public sealed class ProtocolGameT28Tests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static void InvokeHandleRawData(ProtocolGame pg, byte[] data)
    {
        var method = typeof(Protocol).GetMethod(
            "HandleRawData",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method!.Invoke(pg, [data]);
    }

    // ─── parseCoinBalance — update=true ──────────────────────────────────────

    [Fact]
    public void ParseCoinBalance_Update_InvokesEventWithAllCoins()
    {
        using var pg = new ProtocolGame();
        CoinBalance? received = null;
        pg.CoinBalanceReceived += b => received = b;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.CoinBalance);
        out_.WriteU8(1);          // update = true
        out_.WriteU32(10_000);    // coins
        out_.WriteU32(2_500);     // transferableCoins
        out_.WriteU32(500);       // auctionCoins (≥ 1281)

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.NotNull(received);
        Assert.True(received!.IsUpdated);
        Assert.Equal(10_000u, received.Coins);
        Assert.Equal(2_500u,  received.TransferableCoins);
        Assert.Equal(500u,    received.AuctionCoins);
    }

    // ─── parseCoinBalance — update=false ─────────────────────────────────────

    [Fact]
    public void ParseCoinBalance_NoUpdate_InvokesEventWithZeroes()
    {
        using var pg = new ProtocolGame();
        CoinBalance? received = null;
        pg.CoinBalanceReceived += b => received = b;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.CoinBalance);
        out_.WriteU8(0);          // update = false → no further bytes

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.NotNull(received);
        Assert.False(received!.IsUpdated);
        Assert.Equal(0u, received.Coins);
    }

    // ─── parseStoreError ─────────────────────────────────────────────────────

    [Fact]
    public void ParseStoreError_InvokesEvent()
    {
        using var pg = new ProtocolGame();
        byte   recType = 0xFF;
        string recMsg  = string.Empty;
        pg.StoreErrorReceived += (t, m) => { recType = t; recMsg = m; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.StoreError);
        out_.WriteU8(3);
        out_.WriteString("Insufficient coins.");

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(3, recType);
        Assert.Equal("Insufficient coins.", recMsg);
    }

    // ─── parseCoinBalanceUpdating ─────────────────────────────────────────────

    [Fact]
    public void ParseCoinBalanceUpdating_ConsumesOneByte_NoException()
    {
        using var pg = new ProtocolGame();
        bool threw = false;
        try
        {
            var out_ = new OutputMessage();
            out_.WriteU8((byte)GameServerPacket.CoinBalanceUpdating);
            out_.WriteU8(1); // isUpdating byte

            InvokeHandleRawData(pg, out_.ToArray());
        }
        catch
        {
            threw = true;
        }
        Assert.False(threw);
    }

    // ─── parseStore ───────────────────────────────────────────────────────────

    [Fact]
    public void ParseStore_InvokesEvent_WithCategories()
    {
        using var pg = new ProtocolGame();
        IReadOnlyList<StoreCategory>? received = null;
        pg.StoreCategoriesReceived += c => received = c;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.Store);
        out_.WriteU16(2);               // 2 categories

        // category 0
        out_.WriteString("Mounts");     // name
        out_.WriteString("Ride them."); // description (< 1291)
        out_.WriteU8(1);                // state = New
        out_.WriteU8(2);                // 2 icons
        out_.WriteString("mount1.png");
        out_.WriteString("mount2.png");
        out_.WriteString("");           // parent = top-level

        // category 1
        out_.WriteString("Outfits");
        out_.WriteString("Dress up.");
        out_.WriteU8(0);                // state = Normal
        out_.WriteU8(0);                // 0 icons
        out_.WriteString("Cosmetics"); // parent

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.NotNull(received);
        Assert.Equal(2, received!.Count);

        var mounts = received[0];
        Assert.Equal("Mounts", mounts.Name);
        Assert.Equal("Ride them.", mounts.Description);
        Assert.Equal(1, mounts.State);
        Assert.Equal(2, mounts.Icons.Count);
        Assert.Equal("", mounts.Parent);

        var outfits = received[1];
        Assert.Equal("Outfits", outfits.Name);
        Assert.Equal("Cosmetics", outfits.Parent);
    }

    [Fact]
    public void ParseStore_EmptyCategories_InvokesEventWithEmptyList()
    {
        using var pg = new ProtocolGame();
        IReadOnlyList<StoreCategory>? received = null;
        pg.StoreCategoriesReceived += c => received = c;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.Store);
        out_.WriteU16(0); // 0 categories

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.NotNull(received);
        Assert.Empty(received!);
    }

    // ─── parseStoreOffers ─────────────────────────────────────────────────────

    [Fact]
    public void ParseStoreOffers_NormalOffer_InvokesEvent()
    {
        using var pg = new ProtocolGame();
        string? catName = null;
        IReadOnlyList<StoreOffer>? offers = null;
        pg.StoreOffersReceived += (cat, o) => { catName = cat; offers = o; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.StoreOffers);
        out_.WriteString("Mounts");    // category name
        out_.WriteU16(1);             // 1 offer

        // offer
        out_.WriteU32(1234);          // id
        out_.WriteString("Swamp Troll Mount");
        out_.WriteString("A troll.");
        out_.WriteU32(750);           // price
        out_.WriteU8(1);              // state = New (not 2 so no sale fields)
        out_.WriteU8(0);              // not disabled
        out_.WriteU8(1);              // 1 icon
        out_.WriteString("troll.png");
        out_.WriteU16(1);             // 1 sub-offer
        // sub-offer
        out_.WriteString("Swamp Troll");
        out_.WriteString("A swamp troll.");
        out_.WriteU8(0);              // 0 sub-icons
        out_.WriteString("Mount");    // serviceType

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal("Mounts", catName);
        Assert.NotNull(offers);
        Assert.Single(offers!);
        var offer = offers![0];
        Assert.Equal(1234u, offer.Id);
        Assert.Equal("Swamp Troll Mount", offer.Name);
        Assert.Equal(750u, offer.Price);
        Assert.Equal(1, offer.State);
        Assert.False(offer.Disabled);
        Assert.Equal("troll.png", offer.Icon);
        Assert.Single(offer.SubOffers);
        Assert.Equal("Mount", offer.SubOffers[0].ServiceType);
    }

    [Fact]
    public void ParseStoreOffers_SaleOffer_ReadsSaleFields()
    {
        using var pg = new ProtocolGame();
        IReadOnlyList<StoreOffer>? offers = null;
        pg.StoreOffersReceived += (_, o) => offers = o;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.StoreOffers);
        out_.WriteString("Sale");
        out_.WriteU16(1);

        out_.WriteU32(9999);
        out_.WriteString("Sale Item");
        out_.WriteString("On sale!");
        out_.WriteU32(500);
        out_.WriteU8(2);              // STATE_SALE
        out_.WriteU32(1_700_000_000); // saleValidUntil
        out_.WriteU32(1000);          // basePrice
        out_.WriteU8(0);              // not disabled
        out_.WriteU8(0);              // 0 icons
        out_.WriteU16(0);             // 0 sub-offers

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.NotNull(offers);
        var offer = offers![0];
        Assert.Equal(2, offer.State);
        Assert.Equal(1_700_000_000u, offer.SaleValidUntil);
        Assert.Equal(1000u, offer.BasePrice);
    }

    [Fact]
    public void ParseStoreOffers_DisabledOffer_ReadsReason()
    {
        using var pg = new ProtocolGame();
        IReadOnlyList<StoreOffer>? offers = null;
        pg.StoreOffersReceived += (_, o) => offers = o;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.StoreOffers);
        out_.WriteString("Misc");
        out_.WriteU16(1);

        out_.WriteU32(777);
        out_.WriteString("Locked Offer");
        out_.WriteString("Not available.");
        out_.WriteU32(9999);
        out_.WriteU8(0);              // normal state
        out_.WriteU8(1);              // disabled = true
        out_.WriteString("Level requirement not met.");
        out_.WriteU8(0);              // 0 icons
        out_.WriteU16(0);             // 0 sub-offers

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.NotNull(offers);
        Assert.True(offers![0].Disabled);
        Assert.Equal("Level requirement not met.", offers[0].DisabledReason);
    }

    // ─── parseStoreTransactionHistory ────────────────────────────────────────

    [Fact]
    public void ParseStoreTransactionHistory_ConsumesBytes_NoException()
    {
        using var pg = new ProtocolGame();
        bool threw = false;
        try
        {
            var out_ = new OutputMessage();
            out_.WriteU8((byte)GameServerPacket.StoreTransactionHistory);
            out_.WriteU32(1);       // currentPage
            out_.WriteU32(5);       // pageCount
            out_.WriteU8(1);        // 1 entry
            out_.WriteU32(1680000000u); // time
            out_.WriteU8(0);            // productType
            out_.WriteU32(750);         // coinChange
            out_.WriteString("Swamp Troll Mount");

            InvokeHandleRawData(pg, out_.ToArray());
        }
        catch
        {
            threw = true;
        }
        Assert.False(threw);
    }

    // ─── parseCompleteStorePurchase ───────────────────────────────────────────

    [Fact]
    public void ParseCompleteStorePurchase_InvokesEvent()
    {
        using var pg = new ProtocolGame();
        StorePurchaseResult? received = null;
        pg.StorePurchaseCompleted += r => received = r;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.StoreCompletePurchase);
        out_.WriteU8(0);                          // unused byte
        out_.WriteString("Purchase successful!"); // message
        out_.WriteU32(9_250);                     // remainingCoins
        out_.WriteU32(1_500);                     // transferableCoins

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.NotNull(received);
        Assert.Equal("Purchase successful!", received!.Message);
        Assert.Equal(9_250u, received.RemainingCoins);
        Assert.Equal(1_500u, received.TransferableCoins);
    }
}
