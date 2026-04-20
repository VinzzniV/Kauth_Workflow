import { Navigate } from "react-router-dom";

export default function AdminRotationConfigPage() {
  return <Navigate to="/admin/config?section=rotation_requirements" replace />;
}
