# Azure Event Grid Emulator
##### Local development emulator for Azure Event Grid
<sub>_by Peter Barnet, Qualitas Software Ltd, June 2021_<sub>



Handles Azure Event Grid Events in a development/localhost context, without requiring an internet connection or an instance of Azure Event Grid.  This allows disconnected and isolated development use of Event Grid in one or more local running Visual Studio (or VS Code) solutions.

<sub>_Disclaimers_</sub>
<br/><sub>- This is about the art of the possible, and with time it could be made more flexible.</sub>
<br/><sub>- These notes are for developing on Windows, but the concepts can be applied to other environments.</sub>
<br/>
    

## Overview

To allow one or more Visual Studio 2022 C# solutions to send and/or receive pushed events in a local development context.
    
The library Microsoft.Azure.EventGrid requires that events are published as HTTPS.  So the emulator listens for published events on HTTPS, and is addressed with a localhost topic domain hostname.  

The emulator must issue a 200-204 status response once the event is enqueued in the emulator (_ie- it should not wait for push delivery to complete_).  A hosted service will poll the queue, and will distribute these events to the subscriber function(s); the typical scenario is a function app running locally from VS.  
    
There is a basic delivery retry mechanism for the publishing per subscriber and per event, if a push endpoint is not accepting a particular event.

    
## Usage

The emulator listens for events as HTTP requests at: https://localhost:5318/api/events *

A self-signed certificate for localhost emulator will work with the later Microsoft publishing libraries.  

It is probably best to publish the emulator to a folder (eg- 'bin\Publish') and run the .exe from a shortcut (to save memory use); but an instance of VS running the app is also possible.

Events can be published, from a different VS solution, (using package: Azure.Messaging.EventGrid 4.11.0) as follows:

    EventGridPublisherClient client = new(
        new Uri("<topic-uri>"),                   // set to "https://localhost:5318/api/events" in development
        new AzureKeyCredential("<topic-key>"));   // set to Guid.Empty in development
    
    await client.SendEventAsync(egEvent); 

<sub>\* - _By default, but can be adjusted in `appsettings.json` at `Kestrel.Endpoints.Https.Url`._</sub>

## Push Notification Setup

Events published to the emulator will be pushed by the emulator to EventGridTrigger functions, WebHook/Http functions, or any accessible endpoint as defined in the `appsettings.json` file under `Services` as follows:
    
    {
        "Kestrel": ..
        "Logging": ..
        "Services: [
            { <Service> }, .. , { <Service> }
        ]
    }

The structure of a Service is as follows:

    {
        "BaseAddress": "<domain address, eg: http://localhost:7075>",
        "Endpoints" : [
            { <Endpoint> }, .. , { <Endpoint> }
        ]
    }

The structure of an Endpoint is as follows:

    {
        "Path": "<eg: api/my-handler>",   // supply a value for either Path or EventGridFunction
        "EventGridFunction": "<EventGridTrigger Function name, eg: MyAegSubsciptionHandler>",
        "EventTypes": [ "EventType1", .. , "EventTypeN" ]
    }

An event type can be subscribed to by more than one Endpoint or Service.

## Sample Azure Functions Endpoint

The emulator pushes the events to the Azure Functions endpoints over http when running _locally_, which is the normal setup for Azure Functions Core Tools.  Azure Event Grid will always push to Azure deployed function app endpoints using https.  

You will need to ensure _this_ function app is running on port 7075, or adjust the port the emulator tries to write http events to.  
    
_Tip - You can set the port from the function app's context menu by selecting `Properties | Debug | Application Arguments` and entering the following command arguments: `host start --port 7075`_

Setup your C# endpoint function using EventGridTriggerAttribute as follows:

    public class EventHandlerFunctions
    {
        [FunctionName(nameof(MyAegSubsciptionHandler))]
        public async Task MyAegSubsciptionHandler([EventGridTrigger] EventGridEvent @event, ... )
        {
            ...
        }
    }

<sub>_Azure Function EventGrid trigger code above built using package: Microsoft.Azure.WebJobs.Extensions.EventGrid 3.2.0_</sub>

## Events Received and Deadlettered

If you have Azureite, the Microsoft Storage Account Emulator, installed then this emulator will store every event received by appending it to a queue that it creates called `qs-aeg-emulator-received` under the event element, with other details recorded.   The default TTL of 7 days for messages on the queue is used.

If the emulator cannot deliver a message (eg- no subscribers defined for the event type, the maximum no of attempts have been made, and so on) then the event will also be written to another queue called `qs-aeg-emulator-deadletter` under the event element, with other details recorded.  The same TTL applies here.

To disable the use of the storage emulator, add the following to `appsettings.json`:

    {
        "Storage": {
            "Enabled": false
        }
    }

## Coming Next

Watch this space for new features, such as a replay queue for events to be easily resubmitted.