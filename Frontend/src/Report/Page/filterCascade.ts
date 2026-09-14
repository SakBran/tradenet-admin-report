// Client-side cascade helper for dependent report filters (e.g. OGA Section
// depends on OGA Department). Each option carries a `parentId` (the parent
// row's id — for OGA sections this is OGASection.OGADepartmentId). When the
// parent is selected we keep only options whose parentId matches; when the
// parent is unset ("All" = 0 / undefined) we keep every option.
//
// A filter can also cascade from a TEXT parent instead of an id one: the EICC
// Licence/Permit reports narrow Product Group by the card type chosen above it,
// and the card type is a name ("Border Export Licence"), not an id. Those
// options carry a `parentCode` ("Import" / "Export") that the parent's own text
// has to contain — the same test the old admin's eicc-reports.js made in the
// browser (`formType.includes("Export") ? "Export" : "Import"`).
export interface CascadeOption {
  parentId?: number;
  parentCode?: string;
}

export const filterOptionsByParent = <T extends CascadeOption>(
  options: T[],
  parentValue: unknown
): T[] => {
  // Form values can arrive as numbers or numeric strings; 0 / '' / undefined
  // all mean "no parent selected" → show everything.
  const parentId = Number(parentValue);
  if (parentId) {
    return options.filter((option) => option.parentId === parentId);
  }

  // A non-numeric, non-empty parent is a text parent (a card type name).
  const parentText = typeof parentValue === 'string' ? parentValue.trim() : '';
  if (parentText === '') {
    return options;
  }

  return options.filter(
    (option) => !!option.parentCode && parentText.includes(option.parentCode)
  );
};
