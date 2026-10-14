import { useCallback, useEffect, useState, type ChangeEvent, type FormEvent } from 'react';
import { useNavigate } from 'react-router';
import { ApiError, tokenStorage } from '../api/client';
import { exercisesApi, type Exercise } from '../api/endpoints';
import { ErrorMessage, Field, primaryButton } from '../components/ui';

const emptyForm = { name: '', suggestedSets: '3', suggestedRepsPerSet: '10', suggestedRestSeconds: '60' };

function messageOf(err: unknown) {
  return err instanceof ApiError ? err.message : 'Something went wrong. Please try again.';
}

const byName = (a: Exercise, b: Exercise) => a.name.localeCompare(b.name);

export default function ExercisesPage() {
  const navigate = useNavigate();
  const [exercises, setExercises] = useState<Exercise[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const [form, setForm] = useState(emptyForm);
  const [formError, setFormError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      setExercises(await exercisesApi.list());
    } catch (err) {
      setLoadError(messageOf(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  async function handleCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(null);
    setSaving(true);
    try {
      const created = await exercisesApi.create({
        name: form.name.trim(),
        suggestedSets: Number(form.suggestedSets),
        suggestedRepsPerSet: Number(form.suggestedRepsPerSet),
        suggestedRestSeconds: Number(form.suggestedRestSeconds),
      });
      setExercises((current) => [...current, created].sort(byName));
      setForm(emptyForm);
    } catch (err) {
      setFormError(messageOf(err));
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(exercise: Exercise) {
    if (!window.confirm(`Delete "${exercise.name}"?`)) return;
    setActionError(null);
    setDeletingId(exercise.id);
    try {
      await exercisesApi.remove(exercise.id);
      setExercises((current) => current.filter((e) => e.id !== exercise.id));
    } catch (err) {
      setActionError(messageOf(err));
    } finally {
      setDeletingId(null);
    }
  }

  function handleLogout() {
    tokenStorage.clear();
    navigate('/login', { replace: true });
  }

  const update = (field: keyof typeof emptyForm) => (e: ChangeEvent<HTMLInputElement>) =>
    setForm((current) => ({ ...current, [field]: e.target.value }));

  return (
    <div className="min-h-screen">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
          <h1 className="text-xl font-bold text-slate-900">GymNote</h1>
          <button type="button" onClick={handleLogout} className="text-sm font-medium text-slate-600 hover:text-slate-900">
            Log out
          </button>
        </div>
      </header>

      <main className="mx-auto grid max-w-5xl gap-6 px-4 py-6 md:grid-cols-[minmax(0,1fr)_minmax(0,2fr)]">
        <section aria-labelledby="new-exercise" className="h-fit rounded-xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
          <h2 id="new-exercise" className="mb-4 text-lg font-semibold text-slate-900">
            New exercise
          </h2>
          <form onSubmit={handleCreate} className="space-y-4">
            <Field label="Name" name="name" required maxLength={100} value={form.name} onChange={update('name')} />
            <div className="grid grid-cols-3 gap-3">
              <Field label="Sets" name="suggestedSets" type="number" required min={1} max={20}
                value={form.suggestedSets} onChange={update('suggestedSets')} />
              <Field label="Reps / set" name="suggestedRepsPerSet" type="number" required min={1} max={100}
                value={form.suggestedRepsPerSet} onChange={update('suggestedRepsPerSet')} />
              <Field label="Rest (s)" name="suggestedRestSeconds" type="number" required min={0} max={600}
                value={form.suggestedRestSeconds} onChange={update('suggestedRestSeconds')} />
            </div>
            {formError && <ErrorMessage>{formError}</ErrorMessage>}
            <button type="submit" disabled={saving} className={`${primaryButton} w-full`}>
              {saving ? 'Adding…' : 'Add exercise'}
            </button>
          </form>
        </section>

        <section aria-labelledby="my-exercises" className="rounded-xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
          <h2 id="my-exercises" className="mb-4 text-lg font-semibold text-slate-900">
            My exercises
          </h2>

          {actionError && <div className="mb-4"><ErrorMessage>{actionError}</ErrorMessage></div>}

          {loading ? (
            <p role="status" className="py-8 text-center text-sm text-slate-500">Loading exercises…</p>
          ) : loadError ? (
            <div className="space-y-3">
              <ErrorMessage>{loadError}</ErrorMessage>
              <button type="button" onClick={() => void load()} className="text-sm font-medium text-indigo-600 hover:text-indigo-500">
                Try again
              </button>
            </div>
          ) : exercises.length === 0 ? (
            <p className="py-8 text-center text-sm text-slate-500">No exercises yet. Add your first one.</p>
          ) : (
            <ul className="divide-y divide-slate-200">
              {exercises.map((exercise) => (
                <li key={exercise.id} className="flex flex-wrap items-center justify-between gap-3 py-3">
                  <div className="min-w-0">
                    <p className="truncate font-medium text-slate-900">{exercise.name}</p>
                    <p className="text-sm text-slate-500">
                      {exercise.suggestedSets} sets × {exercise.suggestedRepsPerSet} reps · {exercise.suggestedRestSeconds}s rest
                    </p>
                  </div>
                  <button
                    type="button"
                    onClick={() => void handleDelete(exercise)}
                    disabled={deletingId === exercise.id}
                    aria-label={`Delete ${exercise.name}`}
                    className="rounded-md border border-red-200 px-3 py-1.5 text-sm font-medium text-red-600 hover:bg-red-50 disabled:opacity-60"
                  >
                    {deletingId === exercise.id ? 'Deleting…' : 'Delete'}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>
      </main>
    </div>
  );
}
