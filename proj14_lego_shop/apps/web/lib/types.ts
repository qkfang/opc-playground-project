export type Product = {
  id: string;
  name: string;
  theme: string;
  price: number;
  pieces: number;
  ageRange: string;
  rating: number;
  featured: boolean;
  blurb: string;
  description: string;
  colorFrom: string;
  colorTo: string;
};

export type CartItem = {
  productId: string;
  quantity: number;
};

export type Cart = {
  id: string;
  items: CartItem[];
};

export type CartLine = {
  product: Product;
  quantity: number;
  lineTotal: number;
};

export type CartView = {
  id: string;
  lines: CartLine[];
  itemCount: number;
  subtotal: number;
};
