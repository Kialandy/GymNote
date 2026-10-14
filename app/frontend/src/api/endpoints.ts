import { apiFetch } from './client';

export type Exercise = {
  id: string;
  name: string;
  suggestedSets: number;
  suggestedRepsPerSet: number;
  suggestedRestSeconds: number;
};

export type NewExercise = Omit<Exercise, 'id'>;

export const authApi = {
  register: (email: string, password: string) =>
    apiFetch<{ id: string; email: string }>('/auth/register', { method: 'POST', body: { email, password }, auth: false }),
  login: (email: string, password: string) =>
    apiFetch<{ token: string }>('/auth/login', { method: 'POST', body: { email, password }, auth: false }),
};

export const exercisesApi = {
  list: () => apiFetch<Exercise[]>('/exercises'),
  create: (exercise: NewExercise) => apiFetch<Exercise>('/exercises', { method: 'POST', body: exercise }),
  remove: (id: string) => apiFetch<void>(`/exercises/${encodeURIComponent(id)}`, { method: 'DELETE' }),
};
