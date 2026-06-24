// Client-side cascade helper for dependent report filters (e.g. OGA Section
// depends on OGA Department). Each option carries a `parentId` (the parent
// row's id — for OGA sections this is OGASection.OGADepartmentId). When the
// parent is selected we keep only options whose parentId matches; when the
// parent is unset ("All" = 0 / undefined) we keep every option.
export interface CascadeOption {
  parentId?: number;
}

export const filterOptionsByParent = <T extends CascadeOption>(
  options: T[],
  parentValue: unknown
): T[] => {
  // Form values can arrive as numbers or numeric strings; 0 / '' / undefined
  // all mean "no parent selected" → show everything.
  const parentId = Number(parentValue);
  if (!parentId) {
    return options;
  }

  return options.filter((option) => option.parentId === parentId);
};
