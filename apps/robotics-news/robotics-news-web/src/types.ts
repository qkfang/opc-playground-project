export type NewsSource = {
  id: string;
  name: string;
  siteUrl: string;
  feedUrl: string;
};

export type NewsItem = {
  id: string;
  title: string;
  link: string;
  summary: string;
  publishedAtUtc: string;
  sourceId: string;
  author?: string | null;
  tags: string[];
  imageUrl?: string | null;
};

export type NewsResponse = {
  generatedAtUtc: string;
  sources: NewsSource[];
  items: NewsItem[];
};
