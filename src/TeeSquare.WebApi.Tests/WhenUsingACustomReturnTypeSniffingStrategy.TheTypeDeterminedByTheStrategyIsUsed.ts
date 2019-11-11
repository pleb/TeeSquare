// Generated Code

export interface GetRequest<TResponse> {
  url: string;
  method: 'GET';
}
export interface DeleteRequest<TResponse> {
  url: string;
  method: 'DELETE';
}
export interface PostRequest<TRequest, TResponse> {
  data: TRequest;
  url: string;
  method: 'POST';
}
export interface PutRequest<TRequest, TResponse> {
  data: TRequest;
  url: string;
  method: 'PUT';
}
export const toQuery = (o: {[key: string]: any}): string => {
  let q = Object.keys(o)
    .map(k => ({k, v: o[k]}))
    .filter(x => x.v !== undefined && x.v !== null)
    .map(x => `${encodeURIComponent(x.k)}=${encodeURIComponent(x.v)}`)
    .join('&');
  return q && `?${q}` || '';
};
export abstract class RequestFactory {
  static ApiReturnTestGet(): GetRequest<number> {
    return {
      method: 'GET',
      url: `api/return-test`
    };
  }
}
