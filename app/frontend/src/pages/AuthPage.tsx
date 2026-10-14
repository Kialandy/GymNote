import { useState, type FormEvent } from 'react';
import { Navigate, useNavigate } from 'react-router';
import { ApiError, tokenStorage } from '../api/client';
import { authApi } from '../api/endpoints';
import { ErrorMessage, Field, primaryButton } from '../components/ui';

type Mode = 'login' | 'register';

export default function AuthPage() {
  const navigate = useNavigate();
  const [mode, setMode] = useState<Mode>('login');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  if (tokenStorage.get()) return <Navigate to="/exercises" replace />;

  function switchMode(next: Mode) {
    setMode(next);
    setError(null);
    setNotice(null);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setNotice(null);
    setSubmitting(true);
    try {
      if (mode === 'register') {
        await authApi.register(email, password);
        setMode('login');
        setPassword('');
        setNotice('Account created. You can log in now.');
      } else {
        const { token } = await authApi.login(email, password);
        tokenStorage.set(token);
        navigate('/exercises', { replace: true });
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Something went wrong. Please try again.');
    } finally {
      setSubmitting(false);
    }
  }

  const isLogin = mode === 'login';

  return (
    <main className="flex min-h-screen items-center justify-center px-4 py-12">
      <div className="w-full max-w-sm">
        <h1 className="mb-6 text-center text-3xl font-bold text-slate-900">GymNote</h1>
        <div className="rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
          <div className="mb-6 grid grid-cols-2 gap-1 rounded-lg bg-slate-100 p-1" role="group" aria-label="Authentication mode">
            {(['login', 'register'] as const).map((m) => (
              <button
                key={m}
                type="button"
                aria-pressed={mode === m}
                onClick={() => switchMode(m)}
                className={`rounded-md px-3 py-1.5 text-sm font-medium ${
                  mode === m ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-600 hover:text-slate-900'
                }`}
              >
                {m === 'login' ? 'Login' : 'Register'}
              </button>
            ))}
          </div>

          <form onSubmit={handleSubmit} className="space-y-4">
            <Field
              label="Email"
              type="email"
              name="email"
              autoComplete="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
            <Field
              label="Password"
              type="password"
              name="password"
              autoComplete={isLogin ? 'current-password' : 'new-password'}
              required
              minLength={isLogin ? undefined : 8}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
            {!isLogin && <p className="text-xs text-slate-500">At least 8 characters.</p>}

            {error && <ErrorMessage>{error}</ErrorMessage>}
            {notice && (
              <p role="status" className="rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-700">
                {notice}
              </p>
            )}

            <button type="submit" disabled={submitting} className={`${primaryButton} w-full`}>
              {submitting ? 'Please wait…' : isLogin ? 'Log in' : 'Create account'}
            </button>
          </form>
        </div>
      </div>
    </main>
  );
}
