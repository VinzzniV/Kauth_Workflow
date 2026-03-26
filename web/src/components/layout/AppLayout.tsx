import { type ReactNode } from "react";
import { NavLink } from "react-router-dom";
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

  return (
    <div className="app-layout">
      <aside className="sidebar">
        <div className="sidebar-brand">
          <span className="sidebar-logo">MP</span>
          <span className="sidebar-title">Mitarbeiterprozesse</span>
        </div>

        <nav className="sidebar-nav" aria-label="Hauptnavigation">
          {headerNavItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) =>
                `sidebar-link${isActive ? " active" : ""}`
              }
            >
              {item.label}
            </NavLink>
          ))}
        </nav>

        <div className="sidebar-footer">
          {currentUser ? (
            <div className="sidebar-user">
              <div className="sidebar-user-avatar">
                {currentUser.displayName.charAt(0).toUpperCase()}
              </div>
              <div className="sidebar-user-info">
                <p className="sidebar-user-name">{currentUser.displayName}</p>
                {roleLabels.length > 0 ? (
                  <p className="sidebar-user-role">{roleLabels.join(", ")}</p>
                ) : null}
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
      </aside>

      <div className="main-area">
        {children}
      </div>
    </div>
  );
}
