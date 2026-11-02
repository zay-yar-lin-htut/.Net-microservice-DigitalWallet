import type {FastifyRequest} from 'fastify';
import {jwtVerify} from 'jose';
const secret=new TextEncoder().encode(process.env.JWT_SECRET??'');
export async function authenticate(request:FastifyRequest):Promise<string>{const header=request.headers.authorization;if(!header?.startsWith('Bearer '))throw new Error('Unauthenticated.');const {payload}=await jwtVerify(header.slice(7),secret,{algorithms:['HS256'],issuer:process.env.JWT_ISSUER??'DigitalWallet.Identity',audience:process.env.JWT_AUDIENCE??'DigitalWallet.Services'});if(typeof payload.sub!=='string'||!payload.sub)throw new Error('User identifier is missing.');return payload.sub;}
export function assertInternal(request:FastifyRequest){const expected=process.env.INTERNAL_SERVICE_KEY??'';if(!expected||request.headers['x-internal-api-key']!==expected)throw new Error('Internal access denied.');}
