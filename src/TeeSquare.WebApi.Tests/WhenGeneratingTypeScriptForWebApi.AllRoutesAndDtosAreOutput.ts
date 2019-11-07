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
  static ApiOtherDoAThingGet(when?: string): GetRequest<number> {
    let query = toQuery({when});
    return {
      method: 'GET',
      url: `api/other/do-a-thing${query}`
    };
  }
  static ApiRouteNumberOneGet(): GetRequest<string> {
    return {
      method: 'GET',
      url: `api/route-number-one`
    };
  }
  static AltApiRouteNumberTwoByIdGet(id: string): GetRequest<string> {
    return {
      method: 'GET',
      url: `alt-api/route-number-two/${id}`
    };
  }
  static GettitGet(): GetRequest<boolean> {
    return {
      method: 'GET',
      url: `gettit`
    };
  }
  static ApiGet(): GetRequest<any> {
    return {
      method: 'GET',
      url: `api`
    };
  }
  static ApiTestByIdGet(id: number): GetRequest<TestDto> {
    return {
      method: 'GET',
      url: `api/test/${id}`
    };
  }
  static ApiTestPost(data: TestDto): PostRequest<TestDto, number> {
    return {
      method: 'POST',
      data,
      url: `api/test`
    };
  }
  static ApiTestByIdPut(id: number, data: TestDto): PutRequest<TestDto, any> {
    return {
      method: 'PUT',
      data,
      url: `api/test/${id}`
    };
  }
  static ApiValuesGet(): GetRequest<string[]> {
    return {
      method: 'GET',
      url: `api/values`
    };
  }
  static ApiValuesByIdGet(id: number): GetRequest<string> {
    return {
      method: 'GET',
      url: `api/values/${id}`
    };
  }
  static ApiValuesPost(data: string): PostRequest<string, void> {
    return {
      method: 'POST',
      data,
      url: `api/values`
    };
  }
  static ApiValuesByIdPut(id: number, data: string): PutRequest<string, void> {
    return {
      method: 'PUT',
      data,
      url: `api/values/${id}`
    };
  }
  static ApiValuesByIdDelete(id: number): DeleteRequest<void> {
    return {
      method: 'DELETE',
      url: `api/values/${id}`
    };
  }
}
export interface TestDto {
  hello: string;
  count: number;
  createdOn: string;
}
