const aboutCards = [
  {
    title: "What this project is",
    body: "A LEGO marketplace demo paired with a growing set of lightweight showcase pages. About6 is the newest of those simple, self-contained routes.",
  },
  {
    title: "Matches existing page style",
    body: "This page follows the same lightweight content layout already used by the other simple themed routes in the site, so it stays visually consistent.",
  },
  {
    title: "Quick to validate",
    body: "The route stays static and dependency-free so QA can focus on render, navigation, and build success with minimal friction.",
  },
];

const aboutFacts = [
  "About6 reuses the shared app shell and header navigation pattern",
  "A dedicated route makes local render verification simple and obvious",
  "Static content keeps behavior predictable during local QA",
  "The page is low-risk and consistent with prior simple page tasks",
];

export default function About6Page() {
  return (
    <section className="space-y-10">
      <div className="rounded-[2rem] bg-gradient-to-br from-sky-900 via-blue-700 to-indigo-400 px-8 py-12 text-white shadow-lg">
        <p className="mb-3 text-sm font-semibold uppercase tracking-[0.28em] text-sky-100">
          About This Site
        </p>
        <h1 className="text-4xl font-bold tracking-tight sm:text-5xl">About6 Page</h1>
        <p className="mt-4 max-w-2xl text-base leading-7 text-sky-50 sm:text-lg">
          A clean about-style page that fits the site&apos;s existing content pattern and
          is easy to browse, test, and hand through QA.
        </p>
      </div>

      <div className="grid gap-6 md:grid-cols-3">
        {aboutCards.map((card) => (
          <article
            key={card.title}
            className="rounded-3xl border border-sky-100 bg-white p-6 shadow-sm"
          >
            <h2 className="text-xl font-semibold text-slate-900">{card.title}</h2>
            <p className="mt-3 text-sm leading-6 text-slate-700">{card.body}</p>
          </article>
        ))}
      </div>

      <div className="grid gap-8 rounded-[2rem] bg-white p-8 shadow-sm ring-1 ring-sky-100 md:grid-cols-[1.1fr_0.9fr]">
        <div>
          <h2 className="text-2xl font-bold text-slate-900">Why this route fits</h2>
          <p className="mt-4 leading-7 text-slate-700">
            The page adds another simple, self-contained destination to the frontend
            while staying visually consistent with the other showcase-style routes.
            That keeps the user experience familiar and the implementation low-risk.
          </p>
          <p className="mt-4 leading-7 text-slate-700">
            It also makes verification very direct: route render, navigation entry,
            content display, and successful build output.
          </p>
        </div>

        <aside className="rounded-3xl bg-sky-50 p-6 ring-1 ring-sky-100">
          <h3 className="text-lg font-semibold text-slate-900">About6 facts</h3>
          <ul className="mt-4 space-y-3 text-sm leading-6 text-slate-700">
            {aboutFacts.map((fact) => (
              <li key={fact} className="flex gap-3">
                <span className="mt-2 h-2 w-2 rounded-full bg-indigo-400" />
                <span>{fact}</span>
              </li>
            ))}
          </ul>
        </aside>
      </div>

      <div className="rounded-[2rem] border border-dashed border-indigo-300 bg-indigo-50 px-8 py-7">
        <h2 className="text-2xl font-bold text-slate-900">Closing note</h2>
        <p className="mt-3 max-w-3xl leading-7 text-slate-700">
          An about page should feel clear and uncomplicated: one clear route, one clear
          theme, and a smooth trip through build and QA.
        </p>
      </div>
    </section>
  );
}
