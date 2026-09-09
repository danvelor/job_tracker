import { TextInput } from '../atoms/text-input.component';

export function SearchField({
  value,
  onChange,
}: {
  readonly value: string;
  readonly onChange: (value: string) => void;
}) {
  return (
    <TextInput
      testId="filter-search-input"
      id="filter-search"
      value={value}
      onChange={onChange}
      placeholder="Search title or description"
    />
  );
}
