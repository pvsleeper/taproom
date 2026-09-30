export function SkeletonRows() {
  return (
    <div className="overflow-hidden rounded-lg border border-border">
      <table className="w-full min-w-[900px] border-collapse text-sm">
        <tbody>
          {Array.from({ length: 8 }).map((_, i) => (
            <tr key={i} className="h-10 border-b border-border last:border-b-0">
              <td colSpan={9} className="px-3 py-1.5">
                <div className="h-4 w-full animate-pulse rounded bg-surface-hover" />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
