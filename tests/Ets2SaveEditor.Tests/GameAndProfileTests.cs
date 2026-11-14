using Ets2SaveEditor.Core.Game;
using Ets2SaveEditor.Core.Profiles;

namespace Ets2SaveEditor.Tests;

public class GameAndProfileTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(199, 0)]
    [InlineData(200, 1)]
    [InlineData(699, 1)]
    [InlineData(700, 2)]
    [InlineData(799999, 132)]
    public void LevelTable_MapsXpToExpectedLevel(long xp, int expectedLevel)
    {
        Assert.Equal(expectedLevel, LevelTable.Default.LevelFromXp(xp));
    }

    [Fact]
    public void LevelTable_TotalXpForLevel_RoundTrips()
    {
        for (int lvl = 0; lvl <= 40; lvl++)
        {
            var xp = LevelTable.Default.TotalXpForLevel(lvl);
            Assert.Equal(lvl, LevelTable.Default.LevelFromXp(xp));
        }
    }

    [Fact]
    public void DecodeProfileName_DecodesUtf8Hex()
    {
        // "Sergio" -> 53657267696F
        Assert.Equal("Sergio", ProfileLocator.DecodeProfileName("53657267696F"));
        Assert.Equal("not_hex", ProfileLocator.DecodeProfileName("not_hex"));
    }

    [Fact]
    public void Skills_AdrDescribeAndMax()
    {
        Assert.Equal(63, Skills.AdrAll);
        Assert.Equal(6, Skills.AdrClassCount(63));
        Assert.Equal("class 1,2,3,4,6,8", Skills.DescribeAdr(63));
        Assert.Equal("none", Skills.DescribeAdr(0));
    }
}
