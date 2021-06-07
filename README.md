# Azure Event Grid Emulator
##### Local development emulator for Azure Event Grid
<sub>_by Peter Barnet, Qualitas Software Ltd, June 2021_<sub>



Handles Azure Event Grid Events in a development/localhost context, without requiring an internet connection or an instance of Azure Event Grid.  This allows disconnected and isolated development use of Event Grid in one or more local running Visual Studio (or VS Code) solutions.

<sub>_Disclaimers_</sub>
<br/><sub>- This is about the art of the possible, and with time it could be made more flexible.</sub>
<br/><sub>- These notes are for developing on Windows, but the concepts can be applied to other environments.</sub>
<br/>
    

## Overview

To allow one or more Visual Studio 2019 C# solutions to send and/or receive pushed events in a local development context.
    
The library Microsoft.Azure.EventGrid requires that events are published as HTTPS, and to a host name.  So the emulator listens for published events on HTTPS, and is addressed with a topic domain hostname of 'local.eventgrid.net', via proxy.  

The emulator must issue a 202 Accepted response once the event is enqueued in the emulator (_ie- it should not wait for push delivery to complete_).  A hosted service will poll the queue, and will distribute these events to the subscriber function(s); the typical scenario is a function app running locally from VS.  
    
There is a basic delivery retry mechanism for the publishing, if a push endpoint is not accepting the event.

    
## Usage

The emulator listens for events as HTTP requests at: https://localhost:5005/api/events *

It is probably best to publish to folder (eg- 'bin\Publish') and run the .exe from a shortcut (to save memory use), but an instance of VS running the app is also possible.

Events can be published, from a different VS solution, (using package: Microsoft.Azure.EventGrid 3.2.0) as follows:

    var uri = new Uri(options.TopicUri); // set to "https://local.eventgrid.net/api/events" in development
    var key = options.TopicKey;          // set to Guid.Empty in development
    
    var topic = new EventGridClient(new TopicCredentials(key));
    var @event = new EventGridEvent(<id>, <subject>, ...  );
    await topic.PublishEventsAsync(uri.Host, new [] { @event });  
    
Events will be pushed to Azure Function or webhook running on localhost port 7075* as follows:

    http://localhost:7075/runtime/webhooks/eventGrid?functionName={subscriberFunctionName}

Amend the filtering logic in EventProcessor.cs, which currently filters based on three supported EventTypes.

\* - _By default, but can be adjusted in code._


## Setup Proxy & DNS Host Mapping

In an elevated context, add the following entry to the `C:\Windows\System32\drivers\etc\hosts` file:

    127.0.0.183 local.eventgrid.net

In an elevated Command prompt, call the following command:

    netsh interface portproxy add v4tov4 listenport=443 listenaddress=127.0.0.183 connectport=5005 connectaddress=127.0.0.1


## Sample Azure Functions Endpoint

The emulator pushes the events to the Azure Functions endpoints over http when running _locally_, which is the normal setup for Azure Functions Core Tools.  Azure Event Grid will always push to Azure deployed function app endpoints using https.  

You will need to ensure _this_ function app is running on port 7075, or adjust the port the emulator tries to write http events to.

Setup your C# endpoint function using EventGridTriggerAttribute as follows:

    public class EventHandlerFunctions
    {
        [Function(nameof(CreateEventHandler))]
        public async Task CreateEventHandler([EventGridTrigger] EventGridEvent @event, ... )
        {
            ...
        }
    }

<sub>_Azure Function EventGrid trigger code above built using package: Microsoft.Azure.WebJobs.Extensions.EventGrid 2.1.0_</sub>

    



