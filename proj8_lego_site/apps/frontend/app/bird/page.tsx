const birdCards = [
  {
    title: "Light and agile",
    body: "Birds are built for motion and awareness, combining lightweight bodies with fast reactions and striking variety.",
  },
  {
    title: "Consistent site pattern",
    body: "This page follows the same simple content layout used by the other showcase routes already added to the site.",
  },
  {
    title: "Fast to verify",
    body: "The route stays static and dependency-free so QA can focus on the important checks: render, nav, and build success.",
  },
];

const birdFacts = [
  "Birds combine balance, hollow bones, feathers, and strong navigation instincts",
  "A dedicated route makes local render checks quick and obvious",
  "The page uses the shared app shell and common navigation pattern",
  "Static content keeps local verification predictable and low-risk",
];

export default function BirdPage() {
  return (
    <section className="space-y-10">
      <div className="rounded-[2rem] bg-gradient-to-br from-sky-900 via-cyan-600 to-amber-300 px-8 py-12 text-white shadow-lg">
        <p className="mb-3 text-sm font-semibold uppercase tracking-[0.28em] text-sky-100">
          Nature Spotlight
        </p>
        <h1 className="text-4xl font-bold tracking-tight sm:text-5xl">Bird Page</h1>
        <p className="mt-4 max-w-2xl text-base leading-7 text-sky-50 sm:text-lg">
          A bright, clean bird-themed page that matches the existing site pattern and
          is easy to browse, test, and hand through QA.
        </p>
      </div>

      <div className="grid gap-6 md:grid-cols-3">
        {birdCards.map((card) => (
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
            The page adds another self-contained destination to the frontend while
            staying consistent with the other lightweight showcase routes already in the
            app. That keeps the user experience familiar and the build risk low.
          </p>
          <p className="mt-4 leading-7 text-slate-700">
            It also gives the build team a very obvious verification path: route,
            navigation, content render, and successful build output.
          </p>
        </div>

        <aside className="rounded-3xl bg-sky-50 p-6 ring-1 ring-sky-100">
          <h3 className="text-lg font-semibold text-slate-900">Bird facts</h3>
          <ul className="mt-4 space-y-3 text-sm leading-6 text-slate-700">
            {birdFacts.map((fact) => (
              <li key={fact} className="flex gap-3">
                <span className="mt-2 h-2 w-2 rounded-full bg-amber-500" />
                <span>{fact}</span>
              </li>
            ))}
          </ul>
        </aside>
      </div>

      <div className="rounded-[2rem] border border-dashed border-amber-300 bg-amber-50 px-8 py-7">
        <h2 className="text-2xl font-bold text-slate-900">Closing note</h2>
        <p className="mt-3 max-w-3xl leading-7 text-slate-700">
          A bird page should feel light and direct: clear route, clean design, quick
          verification, and no turbulence on the way to QA.
        </p>
      </div>
    </section>
  );
}
