"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useQuery } from "@tanstack/react-query";

import { Logo } from "@/components/logo";
import { NAV_GROUPS, resolveNavLabel } from "@/components/layout/nav-config";
import { getTenantProfile, TENANT_PROFILE_QUERY_KEY } from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { decodeJwtRole } from "@/lib/auth/decode-jwt";
import {
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
} from "@/components/ui/sidebar";

export function AppSidebar() {
  const pathname = usePathname();
  const { session } = useSession();
  const profileQuery = useQuery({
    queryKey: TENANT_PROFILE_QUERY_KEY,
    queryFn: () => getTenantProfile(session!.accessToken),
    enabled: Boolean(session),
  });
  const staffPlural = profileQuery.data?.terminology.staffPlural;
  const isOwner = session ? decodeJwtRole(session.accessToken) === "Owner" : false;

  return (
    <Sidebar collapsible="icon">
      <SidebarHeader>
        <div className="flex items-center px-2 py-1.5 group-data-[collapsible=icon]:justify-center">
          <Logo tagline={false} className="group-data-[collapsible=icon]:[&>span:last-child]:hidden" />
        </div>
      </SidebarHeader>
      <SidebarContent>
        {NAV_GROUPS.map((group) => {
          const items = group.items.filter((item) => !item.ownerOnly || isOwner);
          if (items.length === 0) {
            return null;
          }
          return (
            <SidebarGroup key={group.label} className="px-2 py-1">
              <SidebarGroupLabel className="h-7">{group.label}</SidebarGroupLabel>
              <SidebarGroupContent>
                <SidebarMenu>
                  {items.map((item) => {
                    const label = resolveNavLabel(item.href, item.label, staffPlural);
                    // matchPrefix: Marca tem sub-navegacao interna
                    // (/settings/branding/aparencia etc.) -- sem isto, o item
                    // ficava sem destaque de "ativo" em qualquer sub-aba.
                    const isActive = item.matchPrefix ? pathname.startsWith(item.href) : pathname === item.href;
                    return (
                      <SidebarMenuItem key={item.href}>
                        <SidebarMenuButton
                          asChild
                          isActive={isActive}
                          tooltip={label}
                          className="data-active:bg-primary data-active:text-primary-foreground data-active:hover:bg-primary data-active:hover:text-primary-foreground"
                        >
                          <Link href={item.href}>
                            <item.icon />
                            <span>{label}</span>
                          </Link>
                        </SidebarMenuButton>
                      </SidebarMenuItem>
                    );
                  })}
                </SidebarMenu>
              </SidebarGroupContent>
            </SidebarGroup>
          );
        })}
      </SidebarContent>
    </Sidebar>
  );
}
