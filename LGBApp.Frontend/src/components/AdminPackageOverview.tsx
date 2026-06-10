import { useCallback, useEffect, useState } from 'react';
import { ClipboardList } from 'lucide-react';
import { ApiError, getCustomers, type CustomerPackageDto, type CustomerResponse } from '@/lib/api';

interface AdminPackageOverviewProps {
  refreshKey?: number;
  onManagePackage: (customer: CustomerResponse, pkg: CustomerPackageDto) => void;
}

export function AdminPackageOverview({ refreshKey = 0, onManagePackage }: AdminPackageOverviewProps) {
  const [customers, setCustomers] = useState<CustomerResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      setCustomers(await getCustomers());
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to load customers.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load, refreshKey]);

  const rows = customers.flatMap((customer) =>
    (customer.packages ?? [])
      .filter((p) => p.status === 'Active')
      .map((pkg) => ({ customer, pkg })),
  );

  return (
    <div className="bg-card rounded-lg border border-border overflow-hidden">
      <div className="p-4 border-b border-border flex items-center gap-2">
        <ClipboardList className="w-5 h-5 text-muted-foreground" />
        <div>
          <h2 className="font-medium">Package work overview</h2>
          <p className="text-xs text-muted-foreground mt-0.5">
            Deliverables per company — MOI, MOI Approval, MOA, and services from each purchased package.
          </p>
        </div>
      </div>

      {loading ? (
        <p className="p-6 text-sm text-muted-foreground">Loading packages…</p>
      ) : error ? (
        <p className="p-6 text-sm text-destructive">{error}</p>
      ) : rows.length === 0 ? (
        <p className="p-6 text-sm text-muted-foreground">No active customer packages yet.</p>
      ) : (
        <div className="overflow-auto">
          <table className="w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                <th className="px-4 py-3 text-left">Company</th>
                <th className="px-4 py-3 text-left">Package</th>
                <th className="px-4 py-3 text-left">Validity</th>
                <th className="px-4 py-3 text-left">Expires</th>
                <th className="px-4 py-3 text-right">Value (MYR)</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {rows.map(({ customer, pkg }) => (
                <tr key={`${customer.id}-${pkg.id}`} className="border-t border-border">
                  <td className="px-4 py-3 font-medium">{customer.company}</td>
                  <td className="px-4 py-3">{pkg.packageName}</td>
                  <td className="px-4 py-3">{pkg.validity || '1 Year'}</td>
                  <td className="px-4 py-3">{pkg.expiryDate}</td>
                  <td className="px-4 py-3 text-right">
                    {(pkg.activeValue ?? pkg.packageValue ?? 0).toLocaleString('en-MY', { minimumFractionDigits: 2 })}
                  </td>
                  <td className="px-4 py-3 text-right">
                    <button
                      type="button"
                      onClick={() => onManagePackage(customer, pkg)}
                      className="px-3 py-1.5 text-xs bg-primary text-primary-foreground rounded-lg hover:opacity-90"
                    >
                      Manage package
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
