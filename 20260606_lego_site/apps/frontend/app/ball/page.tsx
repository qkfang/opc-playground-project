const ballCards = [
  {
    title: "Simple and universal",
    body: "A ball is one of the most universal objects there is — easy to recognize, fun to use, and central to countless games.",
  },
  {
    title: "Consistent site pattern",
    body: "This page follows the same lightweight, static layout used by the other simple themed pages already in the site.",
  },
  {
    title: "Fast to validate",
    body: "The route stays dependency-free so QA can focus on the essentials: route render, navigation, and a clean build.",
  },
];

const ballFacts = [
  "Balls appear in sports, play, and physics demonstrations everywhere",
  "A dedicated route makes local render verification quick and obvious",
  "The page uses the shared app shell and common header navigation",
  "Static content keeps behavior predictable during local QA",
];

export default function BallPage() {
  return (
    <section className="space-y-10">
      <div className="rounded-[2rem] bg-gradient-to-br from-red-800 via-orange-500 to-yellow-300 px-8 py-12 text-white shadow-lg">
        <p className="mb-3 text-sm font-semibold uppercase tracking-[0.28em] text-orange-100">
          Object Spotlight
        </p>
        <h1 className="text-4xl font-bold tracking-tight sm:text-5xl">Ball Page</h1>
        <p className="mt-4 max-w-2xl text-base leading-7 text-orange-50 sm:text-lg">
          A bright, simple ball-themed page that fits the site&apos;s existing content
          pattern and is easy to browse, test, and hand through QA.
        </p>
      </div>

      <div className="grid gap-6 md:grid-cols-3">
        {ballCards.map((card) => (
          <article
            key={card.title}
            className="rounded-3xl border border-orange-100 bg-white p-6 shadow-sm"
          >
            <h2 className="text-xl font-semibold text-slate-900">{card.title}</h2>
            <p className="mt-3 text-sm leading-6 text-slate-700">{card.body}</p>
          </article>
        ))}
      </div>

      <div className="grid gap-8 rounded-[2rem] bg-white p-8 shadow-sm ring-1 ring-orange-100 md:grid-cols-[1.1fr_0.9fr]">
        <div>
          <h2 className="text-2xl font-bold text-slate-900">Why this route fits</h2>
          <p className="mt-4 leading-7 text-slate-700">
            The page adds another clear, self-contained destination to the frontend
            while staying visually consistent with the other showcase-style routes.
            That keeps implementation straightforward and the user experience familiar.
          </p>
          <p className="mt-4 leading-7 text-slate-700">
            It also gives the team a very obvious test path: route, navigation, content
            render, and a successful frontend build.
          </p>
        </div>

        <aside className="rounded-3xl bg-orange-50 p-6 ring-1 ring-orange-100">
          <h3 className="text-lg font-semibold text-slate-900">Ball facts</h3>
          <ul className="mt-4 space-y-3 text-sm leading-6 text-slate-700">
            {ballFacts.map((fact) => (
              <li key={fact} className="flex gap-3">
                <span className="mt-2 h-2 w-2 rounded-full bg-red-500" />
                <span>{fact}</span>
              </li>
            ))}
          </ul>
        </aside>
      </div>

      <div className="rounded-[2rem] border border-dashed border-yellow-300 bg-yellow-50 px-8 py-7">
        <h2 className="text-2xl font-bold text-slate-900">Closing note</h2>
        <p className="mt-3 max-w-3xl leading-7 text-slate-700">
          A ball page should feel playful and uncomplicated: clear route, clear theme,
          and a smooth roll through build and QA.
        </p>
      </div>
    </section>
  );
}
