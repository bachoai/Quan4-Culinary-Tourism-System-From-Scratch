export function hasRole(roles: string[] | undefined, expectedRole: string) {
  if (!roles?.length) {
    return false;
  }

  return roles.some((role) => role.trim().toLowerCase() === expectedRole.trim().toLowerCase());
}
