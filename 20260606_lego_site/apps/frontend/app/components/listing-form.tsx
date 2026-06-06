"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { createListing, updateListing } from "@/lib/api";
import type { Listing, ListingInput, LegoSet } from "@/lib/types";

type ListingFormProps = {
  sets: LegoSet[];
  listing?: Listing;
};

const defaultSeller = "demo-user";

export default function ListingForm({ sets, listing }: ListingFormProps) {
  const router = useRouter();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [formData, setFormData] = useState<ListingInput>({
    setId: listing?.setId ?? sets[0]?.id ?? "",
    title: listing?.title ?? "",
    condition: listing?.condition ?? "used",
    price: listing?.price ?? 0,
    currency: listing?.currency ?? "USD",
    description: listing?.description ?? "",
    sellerUserId: listing?.sellerUserId ?? defaultSeller,
    status: listing?.status ?? "active",
  });

  const submitLabel = listing ? "Save listing" : "Create listing";

  async function onSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    setError(null);

    try {
      if (listing) {
        await updateListing(listing.id, formData);
      } else {
        await createListing(formData);
      }

      router.push("/my-listings");
      router.refresh();
    } catch {
      setError("Failed to save listing. Please try again.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <form onSubmit={onSubmit} className="space-y-4 rounded-lg border p-4">
      <label className="block">
        <span className="block text-sm font-medium">Set</span>
        <select
          className="mt-1 w-full rounded border px-3 py-2"
          value={formData.setId}
          onChange={(event) =>
            setFormData((current) => ({ ...current, setId: event.target.value }))
          }
        >
          {sets.map((set) => (
            <option key={set.id} value={set.id}>
              {set.name}
            </option>
          ))}
        </select>
      </label>

      <label className="block">
        <span className="block text-sm font-medium">Title</span>
        <input
          required
          className="mt-1 w-full rounded border px-3 py-2"
          value={formData.title}
          onChange={(event) =>
            setFormData((current) => ({ ...current, title: event.target.value }))
          }
        />
      </label>

      <label className="block">
        <span className="block text-sm font-medium">Condition</span>
        <input
          required
          className="mt-1 w-full rounded border px-3 py-2"
          value={formData.condition}
          onChange={(event) =>
            setFormData((current) => ({ ...current, condition: event.target.value }))
          }
        />
      </label>

      <label className="block">
        <span className="block text-sm font-medium">Price</span>
        <input
          required
          type="number"
          min="0"
          step="0.01"
          className="mt-1 w-full rounded border px-3 py-2"
          value={formData.price}
          onChange={(event) => {
            const parsedPrice = Number.parseFloat(event.target.value);
            setFormData((current) => ({
              ...current,
              price: Number.isNaN(parsedPrice) ? current.price : parsedPrice,
            }));
          }}
        />
      </label>

      <label className="block">
        <span className="block text-sm font-medium">Description</span>
        <textarea
          required
          className="mt-1 w-full rounded border px-3 py-2"
          rows={4}
          value={formData.description}
          onChange={(event) =>
            setFormData((current) => ({
              ...current,
              description: event.target.value,
            }))
          }
        />
      </label>

      {error && <p className="text-sm text-red-600">{error}</p>}

      <button
        type="submit"
        disabled={isSubmitting}
        className="rounded bg-blue-600 px-4 py-2 font-medium text-white disabled:bg-blue-300"
      >
        {isSubmitting ? "Saving..." : submitLabel}
      </button>
    </form>
  );
}
