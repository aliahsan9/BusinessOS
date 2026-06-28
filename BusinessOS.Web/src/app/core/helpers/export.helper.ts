export interface ExportColumn<T> {
  header: string;
  accessor: (row: T) => string | number | boolean | null | undefined;
}

export class ExportHelper {
  static toCsv<T>(rows: T[], columns: ExportColumn<T>[]): string {
    const escape = (value: string): string => {
      if (value.includes(',') || value.includes('"') || value.includes('\n')) {
        return `"${value.replace(/"/g, '""')}"`;
      }
      return value;
    };

    const headerLine = columns.map((c) => escape(c.header)).join(',');
    const dataLines = rows.map((row) =>
      columns
        .map((c) => {
          const raw = c.accessor(row);
          const value = raw === null || raw === undefined ? '' : String(raw);
          return escape(value);
        })
        .join(','),
    );

    return [headerLine, ...dataLines].join('\n');
  }

  static downloadCsv<T>(filename: string, rows: T[], columns: ExportColumn<T>[]): void {
    const csv = `\uFEFF${this.toCsv(rows, columns)}`;
    this.downloadBlob(filename.endsWith('.csv') ? filename : `${filename}.csv`, csv, 'text/csv;charset=utf-8;');
  }

  static downloadExcel<T>(filename: string, rows: T[], columns: ExportColumn<T>[]): void {
    const csv = `\uFEFF${this.toCsv(rows, columns)}`;
    this.downloadBlob(
      filename.endsWith('.xlsx') ? filename : `${filename}.xlsx`,
      csv,
      'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    );
  }

  private static downloadBlob(filename: string, content: string, mimeType: string): void {
    const blob = new Blob([content], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
