import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router';
import { describe, expect, it } from 'vitest';
import { jsonResponse, mockFetch } from '../test/utils';
import AuthPage from './AuthPage';

function renderAuthPage() {
  return render(
    <MemoryRouter initialEntries={['/login']}>
      <Routes>
        <Route path="/login" element={<AuthPage />} />
        <Route path="/exercises" element={<p>Exercises screen</p>} />
      </Routes>
    </MemoryRouter>,
  );
}

async function fillAndSubmit(email: string, password: string, buttonName: RegExp) {
  const user = userEvent.setup();
  await user.type(screen.getByLabelText('Email'), email);
  await user.type(screen.getByLabelText('Password'), password);
  await user.click(screen.getByRole('button', { name: buttonName }));
  return user;
}

describe('AuthPage', () => {
  it('shows the login form by default', () => {
    renderAuthPage();

    expect(screen.getByLabelText('Email')).toBeInTheDocument();
    expect(screen.getByLabelText('Password')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Log in' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Login', pressed: true })).toBeInTheDocument();
  });

  it('logs in, stores the token and navigates to the exercises screen', async () => {
    const fetchMock = mockFetch(() => jsonResponse(200, { token: 'jwt-token' }));
    renderAuthPage();

    await fillAndSubmit('me@example.com', 'Password123!', /log in/i);

    expect(await screen.findByText('Exercises screen')).toBeInTheDocument();
    expect(localStorage.getItem('gymnote.token')).toBe('jwt-token');
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/auth/login');
    expect(init.method).toBe('POST');
    expect(JSON.parse(init.body as string)).toEqual({ email: 'me@example.com', password: 'Password123!' });
  });

  it('shows the server error when login fails and stays on the page', async () => {
    mockFetch(() => jsonResponse(401, { title: 'Invalid email or password.', status: 401 }));
    renderAuthPage();

    await fillAndSubmit('me@example.com', 'wrong-password', /log in/i);

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.');
    expect(screen.queryByText('Exercises screen')).not.toBeInTheDocument();
    expect(localStorage.getItem('gymnote.token')).toBeNull();
  });

  it('registers and switches back to login with a confirmation', async () => {
    const fetchMock = mockFetch(() => jsonResponse(201, { id: '1', email: 'new@example.com' }));
    renderAuthPage();
    await userEvent.click(screen.getByRole('button', { name: 'Register' }));

    await fillAndSubmit('new@example.com', 'Password123!', /create account/i);

    expect(await screen.findByRole('status')).toHaveTextContent('Account created');
    expect(fetchMock.mock.calls[0][0]).toBe('/api/auth/register');
    expect(screen.getByRole('button', { name: 'Log in' })).toBeInTheDocument();
  });

  it('shows a conflict error when the email is already registered', async () => {
    mockFetch(() => jsonResponse(409, { title: 'An account with this email already exists.', status: 409 }));
    renderAuthPage();
    await userEvent.click(screen.getByRole('button', { name: 'Register' }));

    await fillAndSubmit('taken@example.com', 'Password123!', /create account/i);

    expect(await screen.findByRole('alert')).toHaveTextContent('already exists');
  });
});
