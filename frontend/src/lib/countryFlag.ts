export function countryFlag(iso2: string | null): string {
  if (!iso2 || iso2.length !== 2) return ''
  const codePoints = [...iso2.toUpperCase()].map((c) => 0x1f1e6 - 65 + c.charCodeAt(0))
  return String.fromCodePoint(...codePoints)
}
