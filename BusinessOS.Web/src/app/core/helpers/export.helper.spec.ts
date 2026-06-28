import { ExportHelper } from './export.helper';
import { ProductHelper } from './product.helper';

describe('ExportHelper', () => {
  it('should generate CSV with headers and escaped values', () => {
    const csv = ExportHelper.toCsv([{ name: 'A,B', qty: 1 }], [
      { header: 'Name', accessor: (r) => r.name },
      { header: 'Qty', accessor: (r) => r.qty },
    ]);
    expect(csv).toContain('"A,B"');
    expect(csv).toContain('Qty');
  });
});

describe('ProductHelper', () => {
  it('should calculate profit margin', () => {
    expect(ProductHelper.profitMargin(10, 20)).toBe(50);
  });

  it('should calculate stock value', () => {
    expect(ProductHelper.stockValue(5, 10)).toBe(50);
  });
});
