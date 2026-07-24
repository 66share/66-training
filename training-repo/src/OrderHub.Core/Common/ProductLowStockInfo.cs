using OrderHub.Core.Domain;

namespace OrderHub.Core.Common;

public record ProductLowStockInfo(Product Product, int QuantitySoldLast30Days);
