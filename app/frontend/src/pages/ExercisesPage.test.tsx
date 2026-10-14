import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import App from '../App';
import type { Exercise } from '../api/endpoints';
import { jsonResponse, mockFetch } from '../test/utils';

const squat: Exercise = { id: 'a1', name: 'Back Squat', suggestedSets: 4, suggestedRepsPerSet: 8, suggestedRestSeconds: 120 };
const press: Exercise = { id: 'b2', name: 'Overhead Press', suggestedSets: 3, suggestedRepsPerSet: 10, suggestedRestSeconds: 90 };

function renderExercises() {
  return render(
    <MemoryRouter initialEntries={['/exercises']}>
      <App />
    </MemoryRouter>,
  );
}

describe('ExercisesPage', () => {
  beforeEach(() => {
    localStorage.setItem('gymnote.token', 'jwt-token');
  });

  it('shows a loading state, then lists the exercises with the auth header', async () => {
    const fetchMock = mockFetch(() => jsonResponse(200, [squat, press]));
    renderExercises();

    expect(screen.getByRole('status')).toHaveTextContent('Loading exercises');
    const items = await screen.findAllByRole('listitem');

    expect(items).toHaveLength(2);
    expect(items[0]).toHaveTextContent('Back Squat');
    expect(items[0]).toHaveTextContent('4 sets × 8 reps · 120s rest');
    expect(items[1]).toHaveTextContent('Overhead Press');
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/exercises');
    expect((init.headers as Record<string, string>).Authorization).toBe('Bearer jwt-token');
  });

  it('shows an empty state', async () => {
    mockFetch(() => jsonResponse(200, []));
    renderExercises();

    expect(await screen.findByText(/no exercises yet/i)).toBeInTheDocument();
  });

  it('shows an error state with retry when loading fails', async () => {
    let calls = 0;
    mockFetch(() => (++calls === 1 ? jsonResponse(500, { title: 'Server error' }) : jsonResponse(200, [squat])));
    renderExercises();

    expect(await screen.findByRole('alert')).toHaveTextContent('Server error');
    await userEvent.click(screen.getByRole('button', { name: /try again/i }));

    expect(await screen.findByText('Back Squat')).toBeInTheDocument();
  });

  it('creates an exercise and adds it to the list', async () => {
    const fetchMock = mockFetch((_url, init) =>
      init.method === 'POST' ? jsonResponse(201, { ...press, id: 'new' }) : jsonResponse(200, [squat]),
    );
    renderExercises();
    await screen.findByText('Back Squat');
    const user = userEvent.setup();

    await user.type(screen.getByLabelText('Name'), 'Overhead Press');
    await user.clear(screen.getByLabelText('Reps / set'));
    await user.type(screen.getByLabelText('Reps / set'), '10');
    await user.click(screen.getByRole('button', { name: 'Add exercise' }));

    expect(await screen.findByText('Overhead Press')).toBeInTheDocument();
    const post = fetchMock.mock.calls.find(([, init]) => init.method === 'POST')!;
    expect(JSON.parse(post[1].body as string)).toEqual({
      name: 'Overhead Press',
      suggestedSets: 3,
      suggestedRepsPerSet: 10,
      suggestedRestSeconds: 60,
    });
  });

  it('deletes an exercise after confirmation', async () => {
    const fetchMock = mockFetch((_url, init) =>
      init.method === 'DELETE' ? jsonResponse(204) : jsonResponse(200, [squat, press]),
    );
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    renderExercises();
    const row = (await screen.findByText('Back Squat')).closest('li')!;

    await userEvent.click(within(row).getByRole('button', { name: 'Delete Back Squat' }));

    await waitFor(() => expect(screen.queryByText('Back Squat')).not.toBeInTheDocument());
    expect(confirmSpy).toHaveBeenCalledWith('Delete "Back Squat"?');
    expect(fetchMock).toHaveBeenCalledWith('/api/exercises/a1', expect.objectContaining({ method: 'DELETE' }));
    expect(screen.getByText('Overhead Press')).toBeInTheDocument();
  });

  it('does not delete when the confirmation is cancelled', async () => {
    const fetchMock = mockFetch(() => jsonResponse(200, [squat]));
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    renderExercises();

    await userEvent.click(await screen.findByRole('button', { name: 'Delete Back Squat' }));

    expect(screen.getByText('Back Squat')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('redirects to the login screen and clears the token on 401', async () => {
    mockFetch(() => jsonResponse(401));
    renderExercises();

    expect(await screen.findByRole('button', { name: 'Log in' })).toBeInTheDocument();
    expect(localStorage.getItem('gymnote.token')).toBeNull();
  });

  it('redirects to login when there is no token', () => {
    localStorage.clear();
    const fetchMock = mockFetch(() => jsonResponse(200, []));
    renderExercises();

    expect(screen.getByRole('button', { name: 'Log in' })).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
