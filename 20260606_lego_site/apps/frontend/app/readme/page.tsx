const featureCards = [
  {
    title: "What this site is",
    body: "A LEGO marketplace demo: browse sets, view marketplace listings, and manage your own listings — plus a growing set of lightweight showcase pages.",
  },
  {
    title: "How it is built",
    body: "Next.js App Router with a shared header, a common app shell, and Tailwind styling. Pages stay self-contained so each one is easy to render, test, and review.",
  },
  {
    title: "How to read it",
    body: "Use the header navigation to jump between the core marketplace pages and the simple themed routes. This README ties them together in one place.",
  },
];

const coreRoutes = [
  { route: "/", label: "Home", note: "Landing page and entry point." },
  { route: "/sets", label: "Browse Sets", note: "Explore the catalogue of LEGO sets." },
  { route: "/marketplace", label: "Marketplace", note: "Community listings to browse." },
  { route: "/my-listings", label: "My Listings", note: "Listings you own." },
  { route: "/my-listings/new", label: "Create Listing", note: "Add a new listing." },
];

const showcaseRoutes = [
  { route: "/bird", label: "Bird" },
  { route: "/pig", label: "Pig" },
  { route: "/hat", label: "Hat" },
  { route: "/ball", label: "Ball" },
  { route: "/readme", label: "README (this page)" },
];

const runSteps = [
  "Install dependencies once: npm install",
  "Start the dev server: npm run dev",
  "Open the app and use the header nav to explore routes",
  "Verify a production build: npm run build",
];

export default function ReadmePage() {
  return (
    <section className="space-y-10">
      <div className="rounded-[2rem] bg-gradient-to-br from-slate-900 via-blue-800 to-sky-500 px-8 py-12 text-white shadow-lg">
        <p className="mb-3 text-sm font-semibold uppercase tracking-[0.28em] text-sky-100">
          Project Readme
        </p>
        <h1 className="text-4xl font-bold tracking-tight sm:text-5xl">README</h1>
        <p className="mt-4 max-w-2xl text-base leading-7 text-sky-50 sm:text-lg">
          A friendly in-app overview of the LEGO Marketplace site: what it is, how it is
          put together, the routes you can visit, and how to run and build it locally.
        </p>
      </div>

      <div className="grid gap-6 md:grid-cols-3">
        {featureCards.map((card) => (
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
          <h2 className="text-2xl font-bold text-slate-900">Pages in this site</h2>
          <p className="mt-4 leading-7 text-slate-700">
            The site is organised around a small set of core marketplace pages, plus a
            collection of simple themed showcase routes that share the same app shell and
            navigation.
          </p>

          <h3 className="mt-6 text-lg font-semibold text-slate-900">Core pages</h3>
          <ul className="mt-3 space-y-2 text-sm leading-6 text-slate-700">
            {coreRoutes.map((item) => (
              <li key={item.route} className="flex flex-wrap gap-2">
                <code className="rounded bg-slate-100 px-2 py-0.5 font-mono text-xs text-slate-800">
                  {item.route}
                </code>
                <span className="font-medium text-slate-900">{item.label}</span>
                <span className="text-slate-600">— {item.note}</span>
              </li>
            ))}
          </ul>

          <h3 className="mt-6 text-lg font-semibold text-slate-900">Showcase pages</h3>
          <div className="mt-3 flex flex-wrap gap-2">
            {showcaseRoutes.map((item) => (
              <span
                key={item.route}
                className="inline-flex items-center gap-2 rounded-full border border-sky-100 bg-sky-50 px-3 py-1 text-xs font-medium text-slate-800"
              >
                <code className="font-mono text-[0.7rem] text-slate-700">{item.route}</code>
                {item.label}
              </span>
            ))}
          </div>
        </div>

        <aside className="rounded-3xl bg-sky-50 p-6 ring-1 ring-sky-100">
          <h3 className="text-lg font-semibold text-slate-900">Run it locally</h3>
          <ol className="mt-4 space-y-3 text-sm leading-6 text-slate-700">
            {runSteps.map((step, index) => (
              <li key={step} className="flex gap-3">
                <span className="mt-0.5 flex h-5 w-5 flex-none items-center justify-center rounded-full bg-blue-600 text-xs font-semibold text-white">
                  {index + 1}
                </span>
                <span>{step}</span>
              </li>
            ))}
          </ol>
          <p className="mt-5 text-xs leading-5 text-slate-600">
            Built with Next.js (App Router), React, and Tailwind CSS. Pages are static and
            dependency-free where possible to keep local verification fast.
          </p>
        </aside>
      </div>

      <div className="rounded-[2rem] border border-dashed border-sky-300 bg-sky-50 px-8 py-7">
        <h2 className="text-2xl font-bold text-slate-900">At a glance</h2>
        <p className="mt-3 max-w-3xl leading-7 text-slate-700">
          This README page lives inside the app itself, so the overview always travels with
          the site. It follows the same lightweight, self-contained pattern as the other
          showcase routes: a clear theme, the shared header, and a clean, predictable build.
        </p>
      </div>
    </section>
  );
}
