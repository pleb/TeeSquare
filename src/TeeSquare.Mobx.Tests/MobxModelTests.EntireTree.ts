// Auto-generated Code - Do Not Edit

import { types, Instance } from 'mobx-state-tree';

export enum Title {
  Unknown = 0,
  Mr = 1,
  Mrs = 2,
  Miss = 3,
  Doctor = 4,
  Sir = 5,
  Madam = 6
}
export const Name = types.model('Name',  {
  firstName: types.string,
  title: types.frozen<Title>(),
  lastName: types.string,
});

export type NameInstance = Instance<typeof Name>;

export enum Audience {
  Children = 0,
  Teenagers = 1,
  YoungAdults = 2,
  Adults = 3
}
export const Book = types.model('Book',  {
  title: types.string,
  author: Name,
  isAvailable: types.boolean,
  firstPublished: types.Date,
  lastRevisedOn: types.maybe(types.Date),
  reviewedPositively: types.maybe(types.boolean),
  recommendedAudience: types.maybe(types.frozen<Audience>()),
});

export type BookInstance = Instance<typeof Book>;

export const Location = types.model('Location',  {
  latitude: types.number,
  longitude: types.number,
});

export type LocationInstance = Instance<typeof Location>;

export const Library = types.model('Library',  {
  name: types.string,
  location: Location,
  squareMeters: types.integer,
  levels: types.maybe(types.integer),
  allBooks: types.array(Book),
  topBorrowed: types.array(Book),
});

export type LibraryInstance = Instance<typeof Library>;
