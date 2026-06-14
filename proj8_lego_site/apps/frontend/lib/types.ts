export type LegoSet = {
  id: string;
  name: string;
  theme: string;
  year: number;
  pieces: number;
  imageUrl: string;
};

export type Listing = {
  id: string;
  setId: string;
  title: string;
  condition: string;
  price: number;
  currency: string;
  description: string;
  sellerUserId: string;
  createdAt: string;
  status: "active" | "sold";
};

export type ListingInput = Omit<Listing, "id" | "createdAt" | "sellerUserId">;
