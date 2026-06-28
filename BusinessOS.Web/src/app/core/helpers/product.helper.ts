export class ProductHelper {
  static profitMargin(costPrice: number, salePrice: number): number {
    if (!salePrice || salePrice <= 0) {
      return 0;
    }
    return ((salePrice - costPrice) / salePrice) * 100;
  }

  static stockValue(currentStock: number, costPrice: number): number {
    return currentStock * costPrice;
  }

  static profitPotential(currentStock: number, costPrice: number, salePrice: number): number {
    return currentStock * (salePrice - costPrice);
  }

  static formatMargin(margin: number): string {
    return `${margin.toFixed(1)}%`;
  }
}
