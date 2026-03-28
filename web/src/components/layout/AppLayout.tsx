import { type ReactNode, useEffect, useId, useMemo, useRef, useState } from "react";
import { NavLink } from "react-router-dom";
import { useLocation } from "react-router-dom";
import { useAuth } from "../../auth/useAuth";
import { useCurrentUser } from "../../auth/useCurrentUser";
import { useRoleAwareNavigation } from "../../navigation/useRoleAwareNavigation";

type Props = {
  children: ReactNode;
};

export default function AppLayout({ children }: Props) {
  const { logout } = useAuth();
  const { currentUser, roleLabels } = useCurrentUser();
  const { headerNavItems } = useRoleAwareNavigation();
  const location = useLocation();
  const mobileMenuId = useId();
  const mobileMenuButtonRef = useRef<HTMLButtonElement | null>(null);
  const mobileCloseButtonRef = useRef<HTMLButtonElement | null>(null);
  const [isMobileNavOpen, setIsMobileNavOpen] = useState(false);

  useEffect(() => {
    setIsMobileNavOpen(false);
  }, [location.pathname]);

  useEffect(() => {
    if (!isMobileNavOpen) {
      mobileMenuButtonRef.current?.focus();
      return undefined;
    }

    const originalOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const timeoutId = window.setTimeout(() => {
      mobileCloseButtonRef.current?.focus();
    }, 0);
    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setIsMobileNavOpen(false);
      }
    };
    document.addEventListener("keydown", handleEscape);

    return () => {
      window.clearTimeout(timeoutId);
      document.removeEventListener("keydown", handleEscape);
      document.body.style.overflow = originalOverflow;
    };
  }, [isMobileNavOpen]);

  const activeNavLabel = useMemo(() => {
    const activeItem = headerNavItems.find((item) => {
      if (item.end) {
        return location.pathname === item.to;
      }

      return location.pathname === item.to || location.pathname.startsWith(`${item.to}/`);
    });

    return activeItem?.label ?? "Navigation";
  }, [headerNavItems, location.pathname]);

  const sidebarFooter = (
    <div className="sidebar-footer">
      {currentUser ? (
        <div className="sidebar-user">
          <div className="sidebar-user-avatar">
            {currentUser.displayName.charAt(0).toUpperCase()}
          </div>
          <div className="sidebar-user-info">
            <p className="sidebar-user-name">{currentUser.displayName}</p>
            {roleLabels.length > 0 ? <p className="sidebar-user-role">{roleLabels.join(", ")}</p> : null}
          </div>
        </div>
      ) : null}
      <button
        type="button"
        className="sidebar-logout-btn"
        onClick={() => {
          void logout();
        }}
      >
        Abmelden
      </button>
    </div>
  );

  return (
    <div className="app-layout">
      <a href="#main-content" className="skip-link">
        Zum Hauptinhalt springen
      </a>

      <div className="mobile-topbar">
        <div className="mobile-topbar-brand">
          <img
            className="mobile-topbar-logo"
            src="/mitarbeiter_lifecycle_icon.svg"
            alt="Kauth Mitarbeiterprozesse"
          />
          <div className="mobile-topbar-copy">
            <span className="mobile-topbar-title">Kauth Mitarbeiterprozesse</span>
            <span className="mobile-topbar-current">{activeNavLabel}</span>
          </div>
        </div>
        <button
          type="button"
          className="mobile-menu-button"
          aria-expanded={isMobileNavOpen}
          aria-controls={mobileMenuId}
          aria-label={isMobileNavOpen ? "Navigation schließen" : "Navigation öffnen"}
          ref={mobileMenuButtonRef}
          onClick={() => {
            setIsMobileNavOpen((current) => !current);
          }}
        >
          <span className="mobile-menu-button-line" />
          <span className="mobile-menu-button-line" />
          <span className="mobile-menu-button-line" />
        </button>
      </div>

      <div
        className={`mobile-nav-backdrop${isMobileNavOpen ? " is-open" : ""}`}
        aria-hidden={!isMobileNavOpen}
        onClick={() => {
          setIsMobileNavOpen(false);
        }}
      />

      <aside className="sidebar">
        <div className="sidebar-brand">
          <img
            className="sidebar-logo"
            src="/mitarbeiter_lifecycle_icon.svg"
            alt="Kauth Mitarbeiterprozesse"
          />
          <span className="sidebar-title">Kauth Mitarbeiterprozesse</span>
        </div>

        <nav className="sidebar-nav" aria-label="Hauptnavigation">
          {headerNavItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) => `sidebar-link${isActive ? " active" : ""}`}
            >
              <span className="sidebar-link-icon">{item.icon}</span>
              <span className="sidebar-link-label">{item.label}</span>
            </NavLink>
          ))}
        </nav>

        {sidebarFooter}
      </aside>

      <aside
        id={mobileMenuId}
        className={`mobile-nav-drawer${isMobileNavOpen ? " is-open" : ""}`}
        role="dialog"
        aria-modal="true"
        aria-label="Mobile Hauptnavigation"
        aria-hidden={!isMobileNavOpen}
      >
        <div className="mobile-nav-header">
          <div className="sidebar-brand mobile-nav-brand">
            <img
              className="sidebar-logo"
              src="/mitarbeiter_lifecycle_icon.svg"
              alt="Kauth Mitarbeiterprozesse"
            />
            <span className="sidebar-title">Kauth Mitarbeiterprozesse</span>
          </div>
          <button
            type="button"
            className="mobile-nav-close"
            ref={mobileCloseButtonRef}
            onClick={() => {
              setIsMobileNavOpen(false);
            }}
          >
            Schließen
          </button>
        </div>

        <nav className="sidebar-nav" aria-label="Mobile Hauptnavigation">
          {headerNavItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) => `sidebar-link${isActive ? " active" : ""}`}
            >
              <span className="sidebar-link-icon">{item.icon}</span>
              <span className="sidebar-link-label">{item.label}</span>
            </NavLink>
          ))}
        </nav>

        {sidebarFooter}
      </aside>

      <div className="main-area" id="main-content" tabIndex={-1}>
        {children}
      </div>
    </div>
  );
}
