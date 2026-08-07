import { Download } from 'lucide-react';
import { useState } from 'react';
import { ApiError, downloadQuarterlyBillingReport, saveBlobAsFile } from '@/lib/api';

function currentQuarter(): number {
  return Math.floor(new Date().getMonth() / 3) + 1;
}

/** B5 — on-demand quarterly billing report for the Finance Head (who signs in as an Admin). */
export function AdminBillingReports() {
  const now = new Date();
  const [year, setYear] = useState(now.getFullYear());
  const [quarter, setQuarter] = useState(currentQuarter());
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  const years = Array.from({ length: 5 }, (_, i) => now.getFullYear() - i);

  const download = async (format: 'pdf' | 'csv') => {
    setBusy(true);
    setError('');
    try {
      const blob = await downloadQuarterlyBillingReport(year, quarter, format);
      saveBlobAsFile(blob, `LGB-billing-Q${quarter}-${year}.${format}`);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to generate the report.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="bg-card border border-border rounded-lg p-6 space-y-4">
      <div>
        <h3 className="text-lg font-medium">Quarterly billing report</h3>
        <p className="text-sm text-muted-foreground mt-1">
          Invoices raised in the quarter, package contract and remaining value, and package quota
          consumed — one row per customer.
        </p>
      </div>

      {error && <p className="text-sm text-destructive">{error}</p>}

      <div className="flex flex-wrap gap-2 items-center">
        <select
          className="text-sm px-2 py-1.5 border border-border rounded bg-input-background"
          value={quarter}
          onChange={(e) => setQuarter(Number(e.target.value))}
        >
          {[1, 2, 3, 4].map((q) => (
            <option key={q} value={q}>Q{q}</option>
          ))}
        </select>
        <select
          className="text-sm px-2 py-1.5 border border-border rounded bg-input-background"
          value={year}
          onChange={(e) => setYear(Number(e.target.value))}
        >
          {years.map((y) => (
            <option key={y} value={y}>{y}</option>
          ))}
        </select>
        <button
          type="button"
          disabled={busy}
          onClick={() => void download('pdf')}
          className="inline-flex items-center gap-2 text-sm px-3 py-1.5 bg-primary text-primary-foreground rounded disabled:opacity-50"
        >
          <Download className="w-4 h-4" />
          {busy ? 'Generating…' : 'PDF'}
        </button>
        <button
          type="button"
          disabled={busy}
          onClick={() => void download('csv')}
          className="inline-flex items-center gap-2 text-sm px-3 py-1.5 border border-border rounded hover:bg-muted disabled:opacity-50"
        >
          <Download className="w-4 h-4" />
          CSV
        </button>
      </div>
    </div>
  );
}
