using InvestmentTaxCalculator.Enumerations;
using InvestmentTaxCalculator.Model;
using InvestmentTaxCalculator.Model.TaxEvents;

using System.Xml.Linq;

namespace InvestmentTaxCalculator.Parser.InteractiveBrokersXml;

/// <summary>
/// Parses TC (Tender/Conversion) corporate actions from IBKR XML.
/// These represent mergers, acquisitions, and spin-offs where securities are exchanged.
/// Positive quantity entries create acquisition trades with cost basis from the value field.
/// </summary>
public static class IBXmlMergerParser
{
    public static IList<Trade> ParseStockTrades(XElement document)
    {
        // TC = Tender/Conversion (mergers, acquisitions, spin-offs)
        // Only process positive quantity entries (securities received)
        // Only process STK (stocks), not WAR (warrants)
        IEnumerable<XElement> filteredElements = document.Descendants("CorporateAction")
            .Where(row => row.GetAttribute("type") == "TC")
            .Where(row => row.GetAttribute("assetCategory") == "STK")
            .Where(row => IsPositiveQuantity(row))
            .Where(row => HasPositiveValue(row))
            .GroupBy(row => row.GetAttribute("transactionID"))
            .Select(group => group.First());

        return filteredElements.Select(element => XmlParserHelper.ParserExceptionManager(StockTradeMaker, element)).Where(trade => trade != null).ToList()!;
    }

    public static IList<OptionTrade> ParseWarrantTrades(XElement document)
    {
        // Parse WAR (warrants) as OptionTrade to match how warrant sales are parsed
        IEnumerable<XElement> filteredElements = document.Descendants("CorporateAction")
            .Where(row => row.GetAttribute("type") == "TC")
            .Where(row => row.GetAttribute("assetCategory") == "WAR")
            .Where(row => IsPositiveQuantity(row))
            .Where(row => HasPositiveValue(row))
            .GroupBy(row => row.GetAttribute("transactionID"))
            .Select(group => group.First());

        return filteredElements.Select(element => XmlParserHelper.ParserExceptionManager(WarrantTradeMaker, element)).Where(trade => trade != null).ToList()!;
    }

    private static Trade? StockTradeMaker(XElement element)
    {
        decimal quantity = decimal.Parse(element.GetAttribute("quantity"));
        decimal value = decimal.Parse(element.GetAttribute("value"));
        string currency = element.GetAttribute("currency");
        decimal fxRate = decimal.Parse(element.GetAttribute("fxRateToBase"));

        return new Trade
        {
            AcquisitionDisposal = TradeType.ACQUISITION,
            AssetName = element.GetAttribute("symbol"),
            AssetType = AssetCategoryType.STOCK,
            Description = $"Corporate action: {element.GetAttribute("description")}",
            Date = XmlParserHelper.ParseDate(element.GetAttribute("dateTime")),
            Quantity = quantity,
            GrossProceed = new DescribedMoney
            {
                Amount = new WrappedMoney(value, currency),
                Description = "Merger/acquisition cost basis",
                FxRate = fxRate
            },
            Expenses = [],
            TradeReason = TradeReason.CorporateAction
        };
    }

    private static OptionTrade? WarrantTradeMaker(XElement element)
    {
        decimal quantity = decimal.Parse(element.GetAttribute("quantity"));
        decimal value = decimal.Parse(element.GetAttribute("value"));
        string currency = element.GetAttribute("currency");
        decimal fxRate = decimal.Parse(element.GetAttribute("fxRateToBase"));

        // Parse strike price - warrants have strike attribute
        string strikeStr = element.GetAttribute("strike");
        decimal strike = string.IsNullOrEmpty(strikeStr) ? 0 : decimal.Parse(strikeStr);

        // Parse expiry date
        string expiryStr = element.GetAttribute("expiry");
        DateTime expiryDate = string.IsNullOrEmpty(expiryStr) ? DateTime.MaxValue : XmlParserHelper.ParseDate(expiryStr);

        // Parse underlying symbol
        string underlying = element.GetAttribute("underlyingSymbol");

        return new OptionTrade
        {
            AcquisitionDisposal = TradeType.ACQUISITION,
            AssetName = element.GetAttribute("symbol"),
            AssetType = AssetCategoryType.OPTION,
            Description = $"Corporate action: {element.GetAttribute("description")}",
            Date = XmlParserHelper.ParseDate(element.GetAttribute("dateTime")),
            Quantity = quantity,
            GrossProceed = new DescribedMoney
            {
                Amount = new WrappedMoney(value, currency),
                Description = "Merger/acquisition cost basis",
                FxRate = fxRate
            },
            Expenses = [],
            TradeReason = TradeReason.CorporateAction,
            Underlying = underlying,
            StrikePrice = new WrappedMoney(strike, currency),
            ExpiryDate = expiryDate,
            PUTCALL = PUTCALL.CALL, // Warrants are call options
            Multiplier = 1
        };
    }

    private static bool IsPositiveQuantity(XElement element)
    {
        string quantity = element.GetAttribute("quantity");
        return !string.IsNullOrEmpty(quantity) && decimal.TryParse(quantity, out decimal qty) && qty > 0;
    }

    private static bool HasPositiveValue(XElement element)
    {
        string valueStr = element.GetAttribute("value");
        return !string.IsNullOrEmpty(valueStr) && decimal.TryParse(valueStr, out decimal value) && value > 0;
    }
}
