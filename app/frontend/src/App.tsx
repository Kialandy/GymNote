import { useEffect, type ReactNode } from 'react';
import { Navigate, Route, Routes, useNavigate } from 'react-router';
import { setUnauthorizedHandler, tokenStorage } from './api/client';
import AuthPage from './pages/AuthPage';
import ExercisesPage from './pages/ExercisesPage';

function RequireAuth({ children }: { children: ReactNode }) {
  return tokenStorage.get() ? children : <Navigate to="/login" replace />;
}

export default function App() {
  const navigate = useNavigate();

  useEffect(() => {
    setUnauthorizedHandler(() => navigate('/login', { replace: true }));
  }, [navigate]);

  return (
    <Routes>
      <Route path="/login" element={<AuthPage />} />
      <Route
        path="/exercises"
        element={
          <RequireAuth>
            <ExercisesPage />
          </RequireAuth>
        }
      />
      <Route path="*" element={<Navigate to="/exercises" replace />} />
    </Routes>
  );
}
